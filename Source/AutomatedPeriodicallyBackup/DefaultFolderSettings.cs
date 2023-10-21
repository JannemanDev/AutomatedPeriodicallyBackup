using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.IO.Compression;

internal class DefaultFolderSettings
{
    public string FilePattern { get; set; } = "*";
    public bool IncludeSubFolders { get; set; } = true;
    public long MinFileSize { get; set; } = 0;
    public long MaxFileSize { get; set; } = long.MaxValue;

    [JsonConverter(typeof(StringEnumConverter))]
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
}
