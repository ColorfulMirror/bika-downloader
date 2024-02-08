namespace Bika.Downloader.Core.Model;

public record DownloadedComic
{
    public string Id { get; init; }
    public string Title { get; init; }
    public string Author { get; init; }
    public bool Finished { get; init; }
    public List<string> EpisodesId { get; init; }

    public DownloadedComic(Comic comic)
    {
        Id = comic.Id;
        Title = comic.Title;
        Author = comic.Author;
        Finished = comic.Finished;
        EpisodesId = [];
    }
}
