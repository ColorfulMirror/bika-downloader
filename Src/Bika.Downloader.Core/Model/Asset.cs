namespace Bika.Downloader.Core.Model;

// 图片、文件等网络资产
public record struct Asset(string OriginalName, string Path, string FileServer);
