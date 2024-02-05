﻿using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bika.Downloader.Core.Extension;
using Bika.Downloader.Core.Model;
using Microsoft.Extensions.Configuration;

namespace Bika.Downloader.Core;



// bika api文档 https://apifox.com/apidoc/shared-44da213e-98f7-4587-a75e-db998ed067ad
public class BikaService(IConfiguration config)
{
    private readonly HttpClient _httpClient = BikaHttpClientFactory.Create("https://picaapi.picacomic.com/");
    private readonly HttpClient _assetHttpClient= BikaHttpClientFactory.Create("https://storage1.picacomic.com/");
    private readonly JsonSerializerOptions _camelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly JsonSerializerOptions _snakeCaseLower = new()
        { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private List<DownloadedComic>? _downloadedComics;

    public List<DownloadedComic> DownloadedComics
    {
        get
        {
            if (_downloadedComics != null) return _downloadedComics;

            const string filePath = "downloaded.json";
            using FileStream stream = File.Open(filePath, FileMode.OpenOrCreate);
            if (stream.Length == 0)
            {
                byte[] content = "[]"u8.ToArray();
                stream.WriteAsync(content, 0, content.Length);
            }
            List<DownloadedComic> records = JsonSerializer.Deserialize<List<DownloadedComic>>(stream) ?? [];
            _downloadedComics = records;

            return _downloadedComics;
        }
    }

    public async Task LoginAsync()
    {
        var data = new { email = config["username"], password = config["password"] };
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("auth/sign-in", data);
        response.EnsureSuccessStatusCode();


        JsonObject content = await response.Content.ReadFromJsonAsync<JsonObject>() ??
                             throw new Exception("返回值不是一个JSON对象");
        var token = content["data"]["token"].GetValue<string>();

        _httpClient.DefaultRequestHeaders.Add("authorization", token);
    }

    public IAsyncEnumerable<(int, Comic)> GetUserFavoritesAsync()
    {
        return GetAllPageAsync4<Comic>(async page => {
            JsonObject response = await _httpClient.GetFromJsonAsync<JsonObject>($"users/favourite?page={page}") ??
                                  throw new InvalidOperationException("空响应");
            var pages = response["data"]["comics"]["pages"].GetValue<int>();
            Comic[] comics = response["data"]["comics"]["docs"].Deserialize<Comic[]>(_camelCase) ??
                             throw new Exception("没有数据");
            return (pages, comics);
        });
    }

    /**
     * 获取指定ID的Comic
     */
    public async Task<Comic> GetComicAsync(string id)
    {
        JsonObject response = await _httpClient.GetFromJsonAsync<JsonObject>($"comics/{id}") ??
                              throw new InvalidOperationException("空响应");
        return response["data"]["comic"].Deserialize<Comic>(_camelCase);
    }

    /**
     * 接受一个函数，参数是要查询的页数，返回值是总页数+查询页数数据的元组
     * 处理Bika强制分页的接口
     */
    // 这个实现是将所有页的请求进行分组，然后迭代每组，每组内的数据进行并发获取，一组完了执行另外一组
    // 好处是运用了并发。差的是，一组内可能有一个请求特别慢，那么就会阻塞后续请求。并且因为Task的原因必须等到所有数据都settle之后才会返回值
    private async Task<T[]> GetAllPageAsync<T>(Func<int, Task<(int pages, IEnumerable<T> list)>> getPage)
    {
        // 获取第n页的列表数据，丢弃了总页数的一个包裹
        async Task<IEnumerable<T>> GetData(int page)
        {
            (_, IEnumerable<T> data) = await getPage(page);
            return data;
        }

        // 通过手动调用一次得到总页数以及第一页的数据
        (int pages, IEnumerable<T> firstPageData) = await getPage(1);
        // 最终结果
        List<T> results = [..firstPageData];

        if (pages <= 1) return results.ToArray();

        // 将所有页数映射为Task, 然后10个为一组
        IEnumerable<Task<IEnumerable<T>>[]> chunkedTasks = Enumerable.Range(1, pages).Select(GetData).Chunk(10);

        // 并发执行分组内的Task获取数据
        foreach (Task<IEnumerable<T>>[] tasks in chunkedTasks)
        {
            Task<IEnumerable<T>[]> combinedTask = Task.WhenAll(tasks);
            try
            {
                IEnumerable<T>[] remainLists = await combinedTask;
                IEnumerable<T> remainList = remainLists.Aggregate((all, cur) => all.Concat(cur));
                results.AddRange(remainList);
            }
            catch
            {
                foreach (Exception exceptionInnerException in combinedTask.Exception!.InnerExceptions)
                {
                    Console.WriteLine(exceptionInnerException);
                }
            }
        }

        return results.ToArray();
    }

    // 采用SemaphoreSlim限制并发数量，持续保持最大并发数就是10个请求，解决了上面如果一组有一个请求特别慢带来的影响，但是依旧是一个Task
    private async Task<T[]> GetAllPageAsync2<T>(Func<int, Task<(int pages, IEnumerable<T> list)>> getPage)
    {
        (int pages, IEnumerable<T> firstPage) = await getPage(1);
        List<T> result = [..firstPage];

        if (pages <= 1) return result.ToArray();

        SemaphoreSlim semaphore = new(10);
        // 利用WhenALl保证最终得到的数据是连续的
        Task<IEnumerable<T>[]> combinedTask = Task.WhenAll(Enumerable.Range(1, pages).Select(GetPageData));
        try
        {
            IEnumerable<T>[] restPages = await combinedTask;

            foreach (IEnumerable<T> pageData in restPages)
            {
                result.AddRange(pageData);
            }
        }
        catch
        {
            foreach (Exception exceptionInnerException in combinedTask.Exception!.InnerExceptions)
            {
                Console.WriteLine(exceptionInnerException);
            }
        }


        return result.ToArray();

        async Task<IEnumerable<T>> GetPageData(int page)
        {
            // 使用Semaphore限制并发性
            await semaphore.WaitAsync();
            (_, IEnumerable<T> data) = await getPage(page);
            semaphore.Release();
            return data;
        }
    }

    // 这个方法返回一个异步流，请求一页数据，然后迭代该数据yield，然后请求下一页，然后迭代yield...
    // 优点是有了数据就可以第一时间返回，但是获取数据的流程没有并发。所以会一个请求完了再请求另一个效率偏低。
    private async IAsyncEnumerable<T> GetAllPageAsync3<T>(Func<int, Task<(int pages, IEnumerable<T> list)>> getPage)
    {
        (int pages, IEnumerable<T> firstPage) = await getPage(1);

        foreach (T item in firstPage)
        {
            Console.WriteLine("yield data");
            yield return item;
        }

        for (var i = 0; i < pages; i++)
        {
            (_, IEnumerable<T> data) = await getPage(i);
            foreach (T item in data)
            {
                Console.WriteLine("yield data");
                yield return item;
            }
        }
    }

    // 这个方法会异步持续并发10个请求获取数据，存在一个自旋一直迭代yield已获取的数据。
    // 这个比上面实现好的是，会并发获取数据，但是foreach循环体内的代码依旧会阻塞yield的运行
    // 这其实是最佳方案，它本身可以以最快的速读拿到所有数据，并且可以以最快速度迭代第一个数据。
    private async IAsyncEnumerable<(int, T)> GetAllPageAsync4<T>(Func<int, Task<(int pages, IEnumerable<T> list)>> getPage)
    {
        (int totalPage, IEnumerable<T> firstPage) = await getPage(1);

        var result = new IEnumerable<T>?[totalPage];
        result[0] = firstPage;

        SemaphoreSlim semaphore = new(10);

        if (totalPage > 1)
        {
            // 异步但不阻塞
            for (var i = 2; i < totalPage; i++) GetPageData(i);
        }

        var pageIndex = 0;
        var index = 0;
        // 自旋读取result
        while (true)
        {
            // 索引相比页数本就要+1，索引>=总页数 等于当前页已经比总页数大了
            if (pageIndex >= totalPage) break;
            IEnumerable<T>? pageData = result[pageIndex];

            if (pageData == null)
            {
                await Task.Delay(1000);
                continue;
            }

            foreach (T item in pageData)
            {
                yield return (index, item);
                index++;
            }

            pageIndex++;
        }

        yield break;


        void GetPageData(int page)
        {
            semaphore.Wait();
            getPage(page).ContinueWith(t => {
                result[page - 1] = t.Result.list;
                semaphore.Release();
            });
        }

    }

    /**
     * 下载参数提供的本子
     */
    public async Task Download(IEnumerable<Comic> comics)
    {
        foreach (Comic comic in comics)
        {
            await Download(comic);
        }
    }

    public async Task Download(Comic comic)
    {
        SemaphoreSlim semaphore = new(10);

        DownloadedComic? downloadedComic = DownloadedComics.Find(c => c.Id == comic.Id);

        // 下载过了并且没有更新直接跳过下载
        if (downloadedComic != null && comic.EpsCount <= downloadedComic.EpisodesId.Length) return;

        await foreach ((_, Episode episode) in GetEpisodesAsync(comic.Id))
        {
            bool isDownloaded = downloadedComic?.EpisodesId.Contains(episode.Id) ?? false;
            // 该章节下载过了就跳过
            if(isDownloaded) continue;


            Console.WriteLine($"Episode: {episode.Title}");
            // 下载该章节的图片
            await foreach ((int picIndex, Asset picture) in GetEpisodePicturesAsync(comic.Id, episode.Order))
            {
                Console.WriteLine($"index: {picIndex}, picture: {picture}");
            }
        }
    }

    /**
     * 获取指定ID的漫画的所有章节
     */
    public IAsyncEnumerable<(int, Episode)> GetEpisodesAsync(string comicId)
    {
        return GetAllPageAsync4<Episode>(async page => {
            JsonObject response = await _httpClient.GetFromJsonAsync<JsonObject>($"comics/{comicId}/eps?page={page}") ??
                                  throw new InvalidOperationException("空响应");
            var pages = response["data"]["eps"]["pages"].GetValue<int>();
            Episode[] list = response["data"]["eps"]["docs"].Deserialize<Episode[]>(_snakeCaseLower) ??
                             throw new Exception("没有数据");
            return (pages, list);
        });
    }

    public IAsyncEnumerable<(int, Asset)> GetEpisodePicturesAsync(string comicId, int episodeOrder)
    {
        return GetAllPageAsync4<Asset>(async page => {
            JsonObject response =
                await _httpClient.GetFromJsonAsync<JsonObject>(
                    $"comics/{comicId}/order/{episodeOrder}/pages?page={page}") ??
                throw new InvalidOperationException("空响应");
            var pages = response["data"]["pages"]["pages"].GetValue<int>();
            Asset[] list = response["data"]["pages"]["docs"].AsArray()
                                                            .Select(
                                                                 item => item["media"].Deserialize<Asset>(_camelCase))
                                                            .ToArray();
            return (pages, list);
        });
    }

    // 获取目标ID漫画的所有章节所有图片(不推荐使用，章节应该有其必要性)
    public async IAsyncEnumerable<(int, Asset)> GetPicturesAsync(string comicId)
    {
        int index = 0;
        // 两个循环体都不会阻塞内部请求获取，它们会一直获取获取数据，然后有数据就迭代出来，循环体一结束如果有数据就会马上被迭代
        await foreach ((_, Episode episode) in GetEpisodesAsync(comicId))
        await foreach ((_, Asset asset) in GetEpisodePicturesAsync(comicId, episode.Order))
        {
            yield return (index, asset);
            index++;
        }
    }
}
