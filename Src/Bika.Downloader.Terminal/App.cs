using Bika.Downloader.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Bika.Downloader.Terminal;

public class App(IHostApplicationLifetime host, BikaService bikaService, IConfiguration config) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
            string username = config["username"] ?? throw new Exception("未找到username配置项");
            AnsiConsole.Write(new FigletText("Bika-Downloader"));
            // await bikaService.LoginAsync();
            AnsiConsole.Markup($"用户[green]{username}[/]已登录");

        host.StopApplication();
    }
}
