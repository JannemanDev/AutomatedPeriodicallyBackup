using Newtonsoft.Json;
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
    }
}
