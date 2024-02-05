using System.Net.Http.Headers;

namespace Bika.Downloader.Core.Extension;

public static class HttpRequestHeadersExtensions
{
    public static void Set(this HttpRequestHeaders headers, string name, string value)
    {
        if (headers.Contains(name)) headers.Remove(name);
        else headers.Add(name, value);
    }
}
