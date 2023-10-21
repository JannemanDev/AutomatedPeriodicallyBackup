using AutomatedPeriodicallyBackup;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using System.Runtime.Serialization;

partial class Program
{
    public class Settings
    {
        public DefaultFolderSettings DefaultFolderSettings { get; set; }
        public List<FolderProperties> SourceFolders { get; init; } = new List<FolderProperties>();
        public List<FolderProperties> ExcludedFolders { get; init; } = new List<FolderProperties>();
        public string LocalBackupFolder { get; init; } = string.Empty;
        public string RemoteBackupFolder { get; init; } = string.Empty;

        [JsonConverter(typeof(StringEnumConverter))]
        public BackupNotChangedStrategy BackupNotChangedStrategy { get; init; }

        public string PrefixBackupFilename { get; init; } = string.Empty;
        public string SuffixWhenBackupNotChanged { get; init; } = string.Empty;

        [JsonConverter(typeof(StringEnumConverter))]
        public PreserveFolderInArchiveStrategy PreserveFolderInArchiveStrategy { get; init; }

        [JsonConverter(typeof(StringEnumConverter))]
        public BackupFileInUseStrategy BackupFileInUseStrategy { get; init; }

        public bool RunOnce { get; init; }
        public int RunIntervalInMinutes { get; init; }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            Log.Debug("Settings OnDeserialized called");

            SetDefaultsForNullProperties(SourceFolders, "Source");
            SetDefaultsForNullProperties(ExcludedFolders, "Excluded");

            Log.Debug("Settings OnDeserialized ended");
        }

        void SetDefaultsForNullProperties(List<FolderProperties> folders, string folderType)
        {
            foreach (var folder in folders)
            {
                FolderProperties folderBefore = folder.Clone();

                if (folder.FilePattern == null) folder.FilePattern = DefaultFolderSettings.FilePattern;
                if (folder.IncludeSubFolders == null) folder.IncludeSubFolders = DefaultFolderSettings.IncludeSubFolders;
                if (folder.MinFileSize == null) folder.MinFileSize = DefaultFolderSettings.MinFileSize;
                if (folder.MaxFileSize == null) folder.MaxFileSize = DefaultFolderSettings.MaxFileSize;
                if (folder.CompressionLevel == null) folder.CompressionLevel = DefaultFolderSettings.CompressionLevel;

                if (folderBefore != folder)
                {
                    Log.Debug($"One or more properties for {folderType}Folder \"{folder.Folder}\" were not set in the settings json file.");
                    Log.Debug($"Using defaults for those properties.");
                    Log.Debug($"Before:\n{folderBefore.AsJson().IndentLines(" ").TrimEnd()}");
                    Log.Debug($"After:\n{folder.AsJson().IndentLines(" ").TrimEnd()}");
                }
            }
        }
    }
}
