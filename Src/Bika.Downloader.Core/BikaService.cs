using System.Collections;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bika.Downloader.Core.Extension;
using Bika.Downloader.Core.Model;
using Microsoft.Extensions.Configuration;

namespace Bika.Downloader.Core;

// 有请求体的请求添加Content-Type(这里内容必须是application/json; charset=UTF-8  否则bika会报错)
public class ContentTypeHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                           CancellationToken cancellationToken)
    {
        if (request.Content != null)
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json", "UTF-8");

        return base.SendAsync(request, cancellationToken);
    }
}

// 给请求添加signature请求头
public class SignatureHandler : DelegatingHandler
{
    private const string BikaKey = @"~d}$Q7$eIni=V)9\RK/P.RM4;9[7|@/CA}b~OW!3?EV`:<>M7pddUBL5n|0/*Cn";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                                 CancellationToken cancellationToken)
    {
        byte[] key = Encoding.UTF8.GetBytes(BikaKey);
        string path = request.RequestUri?.PathAndQuery.Substring(1) ?? "";
        var time = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        string nonce = request.Headers.GetValues("nonce").FirstOrDefault("");
        HttpMethod method = request.Method;
        string apiKey = request.Headers.GetValues("api-key").FirstOrDefault("");

        // 待加密的数据
        byte[] raw = Encoding.UTF8.GetBytes((path + time + nonce + method + apiKey).ToLower());

        using var hmac = new HMACSHA256(key);
        using MemoryStream ms = new(raw);
        // hmac读取数据流中的数据返回加密后的数据 (和JavaScript不同的是，在.NET中的一些数据总是以流的形式存在，估计是为了通用性，纯粹的数据用流是合适的)
        byte[] signatureBytes = await hmac.ComputeHashAsync(ms, cancellationToken);
        string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

        HttpRequestHeaders headers = request.Headers;

        headers.Set("signature", signature);
        headers.Set("time", time);

        return await base.SendAsync(request, cancellationToken);
    }
}

public class DownloadProgressArgs(string name, int totalPage, int downloadedPage, string author) : EventArgs
{
    public readonly string Name = name;
    public readonly string Author = author;
    public readonly int TotalPage = totalPage;
    public readonly int DownloadedPage = downloadedPage;
    public double Percent => DownloadedPage / TotalPage;

}

// bika api文档 https://apifox.com/apidoc/shared-44da213e-98f7-4587-a75e-db998ed067ad
public class BikaService
{
    private const string BasePath = "https://picaapi.picacomic.com/";
    private const string ApiKey = "C69BAF41DA5ABD1FFEDC6D2FEA56B";
    private const string Nonce = "b1ab87b4800d4d4590a11701b8551afa";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly JsonSerializerOptions _camelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly JsonSerializerOptions _snakeCaseLower = new()
        { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public event EventHandler<DownloadProgressArgs> OnDownloadProgress;

    public BikaService(IConfiguration config, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _config = config;

        InitHttpClient();
    }

    private void InitHttpClient()
    {
        _httpClient.BaseAddress = new Uri(BasePath);
        HttpRequestHeaders headers = _httpClient.DefaultRequestHeaders;
        headers.Add("api-key", ApiKey);
        headers.Add("Accept", "application/vnd.picacomic.com.v1+json");
        headers.Add("app-channel", "1");
        headers.Add("nonce", Nonce);
        headers.Add("app-version", "2.2.1.2.3.3");
        headers.Add("app-uuid", "defaultUuid");
        headers.Add("app-platform", "android");
        headers.Add("app-build-version", "45");
        headers.Add("User-Agent", "okhttp/3.8.1");
        headers.Add("image-quality", "original");
    }

    public async Task LoginAsync()
    {
        var data = new { email = _config["username"], password = _config["password"] };
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("auth/sign-in", data);
        response.EnsureSuccessStatusCode();


        JsonObject content = await response.Content.ReadFromJsonAsync<JsonObject>() ??
                             throw new Exception("返回值不是一个JSON对象");
        var token = content["data"]["token"].GetValue<string>();

        _httpClient.DefaultRequestHeaders.Add("authorization", token);
    }

    public async Task<Comic[]> GetUserFavorites()
    {
        // 传递一个给出页数，返回总页数以及当前页数数据的函数(GetListWithPage)
        Comic[] comics = await GetAllPage<Comic>(async page => {
            JsonObject response = await _httpClient.GetFromJsonAsync<JsonObject>($"users/favourite?page={page}") ??
                                  throw new InvalidOperationException("空响应");
            var pages = response["data"]["comics"]["pages"].GetValue<int>();
            Comic[] comics = response["data"]["comics"]["docs"].Deserialize<Comic[]>(_camelCase) ??
                             throw new Exception("没有数据");
            return (pages, comics);
        });
        return comics;
    }

    public async Task<Comic> GetComic(string id)
    {
        JsonObject response = await _httpClient.GetFromJsonAsync<JsonObject>($"comics/{id}") ??
                              throw new InvalidOperationException("空响应");
        return response["data"]["comic"].Deserialize<Comic>(_camelCase);
    }

    /**
     * 接受一个函数，参数是要查询的页数，返回值是总页数+查询页数数据的元组
     * 处理Bika强制分页的接口
     */
    private async Task<T[]> GetAllPage<T>(Func<int, Task<(int pages, IEnumerable<T> list)>> fn)
    {
        // 获取第n页的列表数据，丢弃了总页数的一个包裹
        async Task<IEnumerable<T>> GetList(int page)
        {
            (_, IEnumerable<T> data) = await fn(page);
            return data;
        }

        // 通过手动调用一次得到总页数以及第一页的数据
        (int pages, IEnumerable<T> results) = await fn(1);

        if (pages <= 1) return results.ToArray();
        // 将所有页数映射为Task, 然后5个为一组分别执行
        IEnumerable<Task<IEnumerable<T>>[]> chunkedTasks = Enumerable.Range(1, pages).Select(GetList).Chunk(5);

        // 执行分组内的Task获取数据
        foreach (Task<IEnumerable<T>>[] tasks in chunkedTasks)
        {
            Task<IEnumerable<T>[]> combinedTask = Task.WhenAll(tasks);
            try
            {
                IEnumerable<T>[] remainLists = await combinedTask;
                IEnumerable<T> remainList = remainLists.Aggregate((all, cur) => all.Concat(cur));
                results = results.Concat(remainList);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        return results.ToArray();
    }

    /**
     * 下载参数提供的本子
     */
    public void Download(Comic[] comics)
    {

    }

    public void Dwonload(Comic comic)
    {

    }

    public Task<Episode[]> GetEpisodes(string comicId)
    {
        return GetAllPage<Episode>(async page => {
            JsonObject response = await _httpClient.GetFromJsonAsync<JsonObject>($"comics/{comicId}/eps?page={page}") ??
                                  throw new InvalidOperationException("空响应");
            var pages = response["data"]["eps"]["pages"].GetValue<int>();
            Episode[] list = response["data"]["eps"]["docs"].Deserialize<Episode[]>(_snakeCaseLower) ??
                             throw new Exception("没有数据");
            return (pages, list);
        });
    }

    public Task<Asset[]> GetEpisodePictures(string comicId, int episodeOrder)
    {
        return GetAllPage<Asset>(async page => {
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

    // 获取目标ID漫画的所有图片
    public async Task<Asset[]> GetPictures(string comicId)
    {
        Episode[] episodes = await GetEpisodes(comicId);

        // 所有章节的所有图片任务
        IEnumerable<Task<Asset[]>> tasks = episodes.Select(episode => GetEpisodePictures(comicId, episode.Order));

        Task<Asset[][]> combinedTask = Task.WhenAll(tasks);
        try
        {
            IEnumerable<Asset>[] allPictureList = await combinedTask;

            Asset[] pictures = allPictureList.Aggregate((all, cur) => all.Concat(cur)).ToArray();
            return pictures;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

    }
}
