using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.IO.Compression;

internal class DefaultFolderSettings
{
    public string[] FilePatterns { get; set; } = new string[] { "*" };
    public string[] IgnoreFilePatterns { get; set; } = new string[] { "" };
    public bool IncludeSubFolders { get; set; } = true;
    public long MinFileSize { get; set; } = 0;
    public long MaxFileSize { get; set; } = long.MaxValue;

    [JsonConverter(typeof(StringEnumConverter))]
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

    public override bool Equals(object obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        DefaultFolderSettings other = (DefaultFolderSettings)obj;

        return
            FilePatterns == other.FilePatterns &&
            IncludeSubFolders == other.IncludeSubFolders &&
            MinFileSize == other.MinFileSize &&
            MaxFileSize == other.MaxFileSize &&
            CompressionLevel == other.CompressionLevel;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FilePatterns, IncludeSubFolders, MinFileSize, MaxFileSize, CompressionLevel);
    }


    //public override int GetHashCode()
    //{
    //    unchecked // Overflow is fine for GetHashCode
    //    {
    //        int hash = 17;
    //        hash = hash * 23 + (FilePattern != null ? FilePattern.GetHashCode() : 0);
    //        hash = hash * 23 + IncludeSubFolders.GetHashCode();
    //        hash = hash * 23 + MinFileSize.GetHashCode();
    //        hash = hash * 23 + MaxFileSize.GetHashCode();
    //        hash = hash * 23 + CompressionLevel.GetHashCode();
    //        return hash;
    //    }
    //}
}
