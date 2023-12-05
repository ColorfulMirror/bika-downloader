using Bika.Downloader.Core;
using Microsoft.Extensions.Hosting;

namespace Bika.Downloader.Terminal ;

    public class App(IHostApplicationLifetime host, BikaService bikaService) : BackgroundService
    {

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await bikaService.LoginAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            host.StopApplication();
        }
    }

