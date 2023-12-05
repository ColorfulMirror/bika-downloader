using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Bika.Downloader.Core.Model;
using Microsoft.Extensions.Configuration;

namespace Bika.Downloader.Core;

public class DefaultHeadersHandler : DelegatingHandler
{
    private const string ApiKey = "C69BAF41DA5ABD1FFEDC6D2FEA56B";
    private const string Nonce = "b1ab87b4800d4d4590a11701b8551afa";
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpRequestHeaders headers = request.Headers;
        headers.Clear();
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
        headers.Add("Content-Type", "application/json; charset=UTF-8");

        if (request.Content != null)

            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json", "UTF-8");

        return base.SendAsync(request, cancellationToken);
    }
}

// 给请求添加signature请求头
public class SignatureHandler : DelegatingHandler
{
    private const string BikaKey = @"~d}$Q7$eIni=V)9\RK/P.RM4;9[7|@/CA}b~OW!3?EV`:<>M7pddUBL5n|0/*Cn";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[] key = Encoding.UTF8.GetBytes(BikaKey);
        string path = request.RequestUri?.PathAndQuery.Substring(1) ?? "";
        var time = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
        // var time = 1701682736.ToString();
        string nonce = request.Headers.GetValues("nonce").FirstOrDefault() ?? "";
        var method = request.Method.ToString();
        string apiKey = request.Headers.GetValues("api-key").FirstOrDefault() ?? "";

        // 待加密的数据
        byte[] raw = Encoding.UTF8.GetBytes((path + time + nonce + method + apiKey).ToLower());

        using var hmac = new HMACSHA256(key);
        using MemoryStream ms = new(raw);
        // hmac读取数据流中的数据返回加密后的数据 (和JavaScript不同的是，在.NET中的一些数据总是以流的形式存在，估计是为了通用性，纯粹的数据用流是合适的)
        byte[] signatureBytes = await hmac.ComputeHashAsync(ms, cancellationToken);
        string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

        HttpRequestHeaders headers = request.Headers;
        headers.Add("signature", signature);
        headers.Add("time", time);

        Console.WriteLine(request.ToString());

        return await base.SendAsync(request, cancellationToken);
    }
}

// bika api文档 https://apifox.com/apidoc/shared-44da213e-98f7-4587-a75e-db998ed067ad
public class BikaService
{
    private const string BasePath = "https://picaapi.picacomic.com/";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public BikaService(IConfiguration config, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _config = config;

        _httpClient.BaseAddress = new Uri(BasePath);
    }

    public async Task LoginAsync()
    {
        var data = new { email = _config["username"], password = _config["password"] };
        using HttpResponseMessage response = await _httpClient.PostAsJsonAsync("auth/sign-in", data);
        // response.EnsureSuccessStatusCode();
        var a = await response.Content.ReadFromJsonAsync<object>();
        Console.WriteLine(a);
    }

    /**
     * 下载和用户配置匹配的本子
     */
    public void Download()
    {
    }

    /**
     * 下载参数提供的本子
     */
    public void Download(Comic[] comics)
    {
    }

    public async void Test()
    {
        // 测试
        using HMACSHA256 hmac = new("1"u8.ToArray());
        using MemoryStream ms = new("2"u8.ToArray());
        byte[] a = await hmac.ComputeHashAsync(ms);

        Console.WriteLine(BitConverter.ToString(a).Replace("-", "").ToLower());


        // 下面案例展示Encoding.GetString和BitConverter.toString区别
        // 字符串utf8编码的字节数组 [41]
        var t1 = "1"u8.ToArray();

        foreach (byte b in t1)
        {
            Console.Write($"{b} ");
        }

        Console.WriteLine("\n------");

        // 值类型转为字节数组 [49, 0]
        foreach (byte b in BitConverter.GetBytes('1'))
        {
            Console.Write($"{b} ");
        }

        Console.WriteLine("\n-----");

        // [41]字节数组变回曾经的原始字符串 [41] => "1"
        Console.WriteLine(Encoding.UTF8.GetString(t1));
        // 将指定字节数组的每个元素的数值转换为其等效的十六进制字符串表示形式。
        // 也就是将字节数组内的值变为16进制，然后数组之间使用'-'连接 [41] => 31
        Console.WriteLine(BitConverter.ToString(t1));
    }
}
