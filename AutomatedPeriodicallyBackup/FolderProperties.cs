using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomatedPeriodicallyBackup
{
    internal class FolderProperties
    {
        public string Folder { get; set; } = "";
        public string? FilePattern { get; set; }
        public bool? IncludeSubFolders { get; set; }
        public long? MinFileSize { get; set; }
        public long? MaxFileSize { get; set; }
        
        [JsonConverter(typeof(StringEnumConverter))]
        public CompressionLevel? CompressionLevel { get; set; }

        public FolderProperties()
        {       
        }

        public FolderProperties(string folder, string? filePattern, bool? includeSubFolders, long? minFileSize, long? maxFileSize, CompressionLevel? compressionLevel)
        {
            Folder = folder;
            FilePattern = filePattern;
            IncludeSubFolders = includeSubFolders;
            MinFileSize = minFileSize;
            MaxFileSize = maxFileSize;
            CompressionLevel = compressionLevel;
        }

        public static FolderProperties CreateFromFolder(string folder)
        {
            return CreateFromFolder(folder, false);
        }        
        
        public static FolderProperties CreateFromFolder(string folder, bool includeSubFolders)
        {
            return new FolderProperties(folder, "*", includeSubFolders, 0, long.MaxValue, System.IO.Compression.CompressionLevel.Optimal);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            FolderProperties other = (FolderProperties)obj;
            return Folder == other.Folder &&
                   FilePattern == other.FilePattern &&
                   IncludeSubFolders == other.IncludeSubFolders &&
                   MinFileSize == other.MinFileSize &&
                   MaxFileSize == other.MaxFileSize &&
                   CompressionLevel == other.CompressionLevel;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 17;
                hashCode = hashCode * 23 + Folder.GetHashCode();
                hashCode = hashCode * 23 + (FilePattern?.GetHashCode() ?? 0);
                hashCode = hashCode * 23 + IncludeSubFolders.GetHashCode();
                hashCode = hashCode * 23 + (MinFileSize?.GetHashCode() ?? 0);
                hashCode = hashCode * 23 + (MaxFileSize?.GetHashCode() ?? 0);
                hashCode = hashCode * 23 + (CompressionLevel?.GetHashCode() ?? 0);
                return hashCode;
            }
        }

        public static bool operator ==(FolderProperties left, FolderProperties right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(FolderProperties left, FolderProperties right)
        {
            return !(left == right);
        }

        public FolderProperties Clone()
        {
            return new FolderProperties
            {
                Folder = this.Folder,
                FilePattern = this.FilePattern,
                IncludeSubFolders = this.IncludeSubFolders,
                MinFileSize = this.MinFileSize,
                MaxFileSize = this.MaxFileSize,
                CompressionLevel = this.CompressionLevel
            };
        }
    }
}
