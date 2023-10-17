using AutomatedPeriodicallyBackup;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Serialization;

partial class Program
{
    public enum BackupNotChangedStrategy
    {
        Keep,
        Delete,
        CreateEmptyBackup,
        CreateEmptyBackupWithSuffix,
    }
    public enum BackupFileInUseStrategy
    {
        Skip,
        TryByMakingCopy,
    }

    public enum PreserveFolderStrategy
    {
        //For example: c:\temp\test1\file.txt
        FullPathWithDrive,  // /c/temp/test1/file.txt
        FullPath,           // /temp/test1/file.txt
        OnlyParentFolder,   // /test1/file.txt
        None,               // /file.txt
    }

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
        public PreserveFolderStrategy PreserveFolderStrategy { get; init; }
               
        [JsonConverter(typeof(StringEnumConverter))]
        public BackupFileInUseStrategy BackupFileInUseStrategy { get; init; }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            Debug.WriteLine("Settings OnDeserialized called");

            foreach (var sourceFolder in SourceFolders)
            {
                if (sourceFolder.FilePattern == null) sourceFolder.FilePattern = DefaultFolderSettings.FilePattern;
                if (sourceFolder.IncludeSubFolders == null) sourceFolder.IncludeSubFolders = DefaultFolderSettings.IncludeSubFolders;
                if (sourceFolder.MinFileSize == null) sourceFolder.MinFileSize = DefaultFolderSettings.MinFileSize;
                if (sourceFolder.MaxFileSize == null) sourceFolder.MaxFileSize = DefaultFolderSettings.MaxFileSize;
                if (sourceFolder.CompressionLevel == null) sourceFolder.CompressionLevel = DefaultFolderSettings.CompressionLevel;
            }

            foreach (var excludedFolder in ExcludedFolders)
            {
                if (excludedFolder.FilePattern == null) excludedFolder.FilePattern = DefaultFolderSettings.FilePattern;
                if (excludedFolder.IncludeSubFolders == null) excludedFolder.IncludeSubFolders = DefaultFolderSettings.IncludeSubFolders;
                if (excludedFolder.MinFileSize == null) excludedFolder.MinFileSize = DefaultFolderSettings.MinFileSize;
                if (excludedFolder.MaxFileSize == null) excludedFolder.MaxFileSize = DefaultFolderSettings.MaxFileSize;
                if (excludedFolder.CompressionLevel == null) excludedFolder.CompressionLevel = DefaultFolderSettings.CompressionLevel;
            }
        }
    }
}
