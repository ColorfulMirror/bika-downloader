namespace Bika.Downloader.Core.Model;

public record DownloadedComic()
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public bool Finished { get; set; }
    public string[] EpisodesId { get; set; }
}
