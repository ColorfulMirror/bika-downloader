using Bika.Downloader.Core;
using Bika.Downloader.Terminal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

// 配置.NET Host，依赖注入以及配置
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
IServiceCollection services = builder.Services;
ConfigurationManager config = builder.Configuration;

services.AddHostedService<App>();
services.AddTransient<SignatureHandler>();
services.AddTransient<ContentTypeHandler>();
services.AddHttpClient<BikaService>()
        .AddHttpMessageHandler<ContentTypeHandler>()
        .AddHttpMessageHandler<SignatureHandler>();


// 加载home目录下的配置文件
const string configFile = "bika-downloader.config.json";
var provider = new PhysicalFileProvider(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
config.AddJsonFile(provider, configFile, true, true);


using IHost host = builder.Build();
host.Run();
