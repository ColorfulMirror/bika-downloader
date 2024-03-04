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
 * 单行本 58777ebd6eb21f2a740fa6dc,
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


        // List<Comic> favoriteComics = bikaService.GetUserFavoritesAsync()
        //                                                .ToBlockingEnumerable(stoppingToken)
        //                                                .Select(tuple => tuple.Item2).ToList();
        Comic comic = await bikaService.GetComicAsync("5884941e3f65ce7fcdd5be87");
        Comic comic2 = await bikaService.GetComicAsync("58777ebd6eb21f2a740fa6db");
        List<Comic> favoriteComics = [comic, comic2];
        List<DownloadedComic> downloadedComics = (await bikaService.GetDownloadedComics()).ToList();
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


        // await bikaService.Download(comic);
        //
        if (unDownloadedComics.Count != 0)
        {
            AnsiConsole.MarkupLine("新下载漫画：");
            await AnsiConsole.Progress()
                             .StartAsync(async ctx => {
                                  Dictionary<string, ProgressTask> hash = new();
                                  Progress<DownloadProgress> progress = new(p => {
                                      if (!hash.TryGetValue(p.comicId, out ProgressTask? value)) return;
                                      value.Value = p.progress;
                                  });

                                  foreach (Comic unDownloadComic in unDownloadedComics)
                                  {
                                      ProgressTask task = ctx.AddTask(unDownloadComic.Title, true, 1);
                                      hash.Add(unDownloadComic.Id, task);
                                  }

                                  foreach (Comic unDownloadComic in unDownloadedComics)
                                  {
                                      await bikaService.Download(unDownloadComic, progress);
                                  }
                              });
        }

        if (updatedComics.Count != 0)
        {
            AnsiConsole.MarkupLine("更新的漫画：");
            await AnsiConsole.Progress()
                             .StartAsync(async ctx => {
                                  Dictionary<string, ProgressTask> hash = new();
                                  Progress<DownloadProgress> progress = new(p => {
                                      if (!hash.TryGetValue(p.comicId, out ProgressTask? value)) return;
                                      value.Value = p.progress;
                                  });

                                  foreach (Comic updatedComic in updatedComics)
                                  {
                                      ProgressTask task = ctx.AddTask(updatedComic.Title, true, 1);
                                      hash.Add(updatedComic.Id, task);
                                  }

                                  foreach (Comic updatedComic in updatedComics)
                                  {
                                      await bikaService.Download(updatedComic, progress);
                                  }
                              });

        }

        host.StopApplication();
    }
}
