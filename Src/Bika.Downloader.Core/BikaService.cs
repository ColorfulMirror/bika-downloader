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


        JsonObject content = await response.Content.ReadFromJsonAsync<JsonObject>() ?? throw new Exception("返回值不是一个JSON对象");
        string token = content["data"]["token"].GetValue<string>();

        _httpClient.DefaultRequestHeaders.Add("authorization", token);
    }

    public async Task<IEnumerable<Comic>> GetUserFavorites()
    {
        // 获取收藏夹的总页数，bika后端做了强制分页
        async Task<int> GetFavoritePages()
        {
            JsonObject response = await _httpClient.GetFromJsonAsync<JsonObject>("users/favourite") ?? throw new Exception("空返回值");
            return response["data"]["comics"]["pages"].GetValue<int>();
        }

        async Task<IEnumerable<Comic>> GetFavorites(int page)
        {
            JsonObject response = await _httpClient.GetFromJsonAsync<JsonObject>($"users/favourite?page={page}") ?? throw new Exception("空返回值");
            return JsonSerializer.Deserialize<Comic[]>(response["data"]["comics"]["docs"]);
        }

        int pages = await GetFavoritePages();

        IEnumerable<Task<IEnumerable<Comic>>> tasks = Enumerable.Range(1, pages).Select(GetFavorites);

        Task<IEnumerable<Comic>[]> combinedTask = Task.WhenAll(tasks);

        try
        {
            IEnumerable<Comic>[] allComics = await combinedTask;
            return allComics.Aggregate((all, comics) => all.Concat(comics));
        }
        catch
        {
            if (combinedTask.Exception?.InnerExceptions != null)
                foreach (Exception e in combinedTask.Exception.InnerExceptions)
                {
                    Console.WriteLine(e);

                    throw e;
                }

            throw;
        }

    }

    /**
     * 下载参数提供的本子
     */
    public void Download(Comic[] comics)
    {
    }

}
