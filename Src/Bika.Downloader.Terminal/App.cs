using Bika.Downloader.Core;
using Bika.Downloader.Core.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace Bika.Downloader.Terminal;

public class App(IHostApplicationLifetime host, BikaService bikaService, IConfiguration config) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string username = config["username"] ?? throw new Exception("未找到username配置项");
        AnsiConsole.Write(new FigletText("Bika-Downloader"));
        await bikaService.LoginAsync();
        AnsiConsole.Markup($"用户[green]{username}[/]已登录");
        IEnumerable<Comic> comics = await bikaService.GetUserFavorites();

        host.StopApplication();
    }
}
