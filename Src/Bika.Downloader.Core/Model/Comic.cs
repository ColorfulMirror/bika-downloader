using System.Text.Json.Serialization;

namespace Bika.Downloader.Core.Model;


public record struct Comic([property: JsonPropertyName("_id")] string Id, string Title, Asset Asset, string Author, bool Finished, int EpsCount);
