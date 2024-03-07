using Bika.Downloader.Core;
using Bika.Downloader.Terminal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// 配置.NET Host，依赖注入以及配置
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
IServiceCollection services = builder.Services;
ConfigurationManager config = builder.Configuration;

services.AddHostedService<App>();
services.AddTransient<BikaService>();

// 加载home目录下的配置文件
const string configFile = "bika-downloader.config.json";
var provider = new PhysicalFileProvider(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
config.AddJsonFile(provider, configFile, true, true);

// Microsoft命名空间下的日志只输出Error以上的
builder.Logging.AddFilter("Microsoft", LogLevel.Error);

using IHost host = builder.Build();
host.Run();
