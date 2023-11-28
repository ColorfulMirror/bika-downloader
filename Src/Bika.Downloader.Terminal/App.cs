using Bika.Downloader.Core;
using Microsoft.Extensions.Hosting;

namespace Bika.Downloader.Terminal ;

    public class App(IHostApplicationLifetime hostApplicationLifetime, BikaService bikaService) : BackgroundService
    {

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            bikaService.Login();
            await Task.Delay(100, stoppingToken);
            Console.WriteLine("1234");
            hostApplicationLifetime.StopApplication();
        }
    }

