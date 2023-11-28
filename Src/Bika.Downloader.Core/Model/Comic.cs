using System.Text.Json.Serialization;

namespace Bika.Downloader.Core.Model ;

    public record struct Thumb(string OriginalName, string Path, string FileServer);

    public record struct Comic
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; }
        public string Title { get; set; }
        public Thumb Thumb { get; set; }
        public string Author { get; set; }
        public bool Finished { get; set; }
        public int EpsCount { get; set; }
    }

