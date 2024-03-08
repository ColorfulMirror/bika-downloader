using System.Text.RegularExpressions;
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
 *
 * おさえきれないこの情欲 58777ebd6eb21f2a740fa6db
 * 单行本 58777ebd6eb21f2a740fa6dc
 */
public partial class App(IHostApplicationLifetime host, BikaService bikaService, IConfiguration config) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.CancelKeyPress += (_, args) => {
            args.Cancel = true;
            host.StopApplication();
        };

        AnsiConsole.Write(new FigletText("Bika-Downloader").Centered().Color(Color.Pink3));
        AnsiConsole.WriteLine("程序开始运行，使用Ctrl+C可关闭下载程序");
        await AnsiConsole.Status()
                         .StartAsync("正在登录Bika...", _ => bikaService.LoginAsync());
        string username = config["username"] ?? throw new Exception("未找到username配置项");
        AnsiConsole.MarkupLine($"用户[green]{username}[/]登录成功");

        List<Comic> favoriteComics = await AnsiConsole.Status()
                                                      .StartAsync("正在获取收藏夹漫画...", _ => Task.FromResult(
                                                                      bikaService.GetUserFavoritesAsync()
                                                                                 .ToBlockingEnumerable(stoppingToken)
                                                                                 .Select(tuple => tuple.Item2)
                                                                                 .ToList()
                                                                  ));


        List<DownloadedComic> downloadedComics = await AnsiConsole.Status()
                                                                  .StartAsync("正在获取已经下载过的漫画...",
                                                                              async _ => (await bikaService.GetDownloadedComics())
                                                                                 .ToList());
        List<Comic> unDownloadedComics = favoriteComics.ExceptBy(downloadedComics.Select(c => c.Id), c => c.Id).ToList();
        List<Comic> updatedComics = (
                                        from fComic in favoriteComics
                                        join dComic in downloadedComics on fComic.Id equals dComic.Id
                                        where fComic.EpsCount > dComic.EpisodesId.Count
                                        select fComic
                                    ).ToList();

        AnsiConsole.MarkupLine(
            $"[red]新下载[/]漫画[green]{unDownloadedComics.Count}[/]本, [red]更新的[/]漫画[green]{updatedComics.Count}[/]本"
        );


        if (unDownloadedComics.Count != 0)
        {
            AnsiConsole.MarkupLine("\n新下载漫画：");
            await DownloadComics(unDownloadedComics);
        }

        if (updatedComics.Count != 0)
        {
            AnsiConsole.MarkupLine("\n更新的漫画：");
            await DownloadComics(updatedComics);
        }

        host.StopApplication();
        return;

        async Task DownloadComics(IEnumerable<Comic> comics)
        {
            Regex regex = SquareBracketRegex();
            List<Comic[]> chunkedComics = comics.Chunk(10).ToList();
            AnsiConsole.MarkupLine($"共分[green]{chunkedComics.Count}[/]个分块");
            foreach ((Comic[] chunkedComic, int i) in chunkedComics.Select((cs, i) => (cs, i)))
            {
                AnsiConsole.MarkupLine($"现在下载第[green]{i+1}[/]分块");
                await AnsiConsole.Progress().StartAsync(async ctx => {
                    Dictionary<string, ProgressTask> hash = new();
                    Progress<DownloadProgress> progress = new(p => {
                        if (!hash.TryGetValue(p.comicId, out ProgressTask? value)) return;
                        value.Value = p.progress;
                    });
                    foreach (Comic comic in chunkedComic)
                    {
                        string title = regex.Replace(comic.Title, "");
                        title = title.Trim();
                        if(title.Length > 15) title = title[..14] + "...";
                        ProgressTask task = ctx.AddTask(title, true, 1);
                        hash.Add(comic.Id, task);
                    }
                    foreach (Comic comic in chunkedComic)
                    {
                        await bikaService.Download(comic, progress);
                    }
                });
            }
        }
    }

    [GeneratedRegex(@"\[.*\]")]
    private static partial Regex SquareBracketRegex();
}
