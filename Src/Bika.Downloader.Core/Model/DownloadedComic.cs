namespace Bika.Downloader.Core.Model;

public record DownloadedComic()
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public bool Finished { get; set; }
    public List<string> EpisodesId { get; set; }

    public DownloadedComic(Comic comic) : this()
    {
        Id = comic.Id;
        Title = comic.Title;
        Author = comic.Author;
        Finished = comic.Finished;
        EpisodesId = [];
    }
}
