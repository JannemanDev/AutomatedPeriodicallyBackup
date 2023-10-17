using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutomatedPeriodicallyBackup
{
    internal class DefaultFolderSettings
    {
        public string FilePattern { get; set; } = "*";
        public bool IncludeSubFolders { get; set; } = true;
        public long MinFileSize { get; set; } = 0;
        public long MaxFileSize { get; set; } = long.MaxValue;
        public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;
    }
}
