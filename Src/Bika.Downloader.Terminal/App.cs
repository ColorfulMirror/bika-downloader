using System.Text.Json;
using System.Text.Json.Nodes;
using Bika.Downloader.Core;
using Bika.Downloader.Core.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace Bika.Downloader.Terminal;

/**
 * 测试数据
 * Comic: Hな年上の人妻・女上司本 ID: 5884941e3f65ce7fcdd5be87
 * Episodes: 588d62e73f65ce7fcdd60a27 贈品本 ; 5884941e3f65ce7fcdd5be88 第1集
 */
public class App(IHostApplicationLifetime host, BikaService bikaService, IConfiguration config) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string username = config["username"] ?? throw new Exception("未找到username配置项");
        AnsiConsole.Write(new FigletText("Bika-Downloader"));

        await AnsiConsole.Status()
                         .StartAsync("正在登录Bika...", _ => bikaService.LoginAsync());
        AnsiConsole.MarkupLine($"用户[green]{username}[/]登录成功");


        // await foreach (Comic comic in bikaService.GetUserFavoritesAsync().WithCancellation(stoppingToken))
        // {
        //     Console.WriteLine($"foreach inner, {comic.Title}， {comic.Id}");
        // }

        await foreach (Asset asset in bikaService.GetPicturesAsync("5884941e3f65ce7fcdd5be87").WithCancellation(stoppingToken))
        {
            Console.WriteLine(asset);
        }

        host.StopApplication();
    }
}
