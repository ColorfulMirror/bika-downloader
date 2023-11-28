using Bika.Downloader.Core.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Bika.Downloader.Core;

// bika api文档 https://apifox.com/apidoc/shared-44da213e-98f7-4587-a75e-db998ed067ad
public class BikaService(IOptions<Config> config, IConfiguration configuration)
{
    public Config Config { get; } = config.Value;

    public void Login()
    {
        Console.WriteLine($"Login, {configuration["Logging"]}");
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
}
