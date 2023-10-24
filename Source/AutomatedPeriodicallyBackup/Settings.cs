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

        [JsonConverter(typeof(TimeSpanJsonConverter))] // Apply the custom converter
        public TimeSpan RunInterval { get; init; }

        [JsonConverter(typeof(TimeSpanJsonConverter))] // Apply the custom converter
        public TimeSpan DeleteBackupsWhenOlderThan { get; set; }

        public int MinimumBackupsToKeep { get; set; }

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

        public Settings Clone()
        {
            return new Settings
            {
                DefaultFolderSettings = DefaultFolderSettings,
                SourceFolders = SourceFolders.Select(f => f.Clone()).ToList(),
                ExcludedFolders = ExcludedFolders.Select(f => f.Clone()).ToList(),
                LocalBackupFolder = LocalBackupFolder,
                RemoteBackupFolder = RemoteBackupFolder,
                BackupNotChangedStrategy = BackupNotChangedStrategy,
                PrefixBackupFilename = PrefixBackupFilename,
                SuffixWhenBackupNotChanged = SuffixWhenBackupNotChanged,
                PreserveFolderInArchiveStrategy = PreserveFolderInArchiveStrategy,
                BackupFileInUseStrategy = BackupFileInUseStrategy,
                RunOnce = RunOnce,
                RunInterval = RunInterval,
                DeleteBackupsWhenOlderThan = DeleteBackupsWhenOlderThan,
                MinimumBackupsToKeep = MinimumBackupsToKeep
            };
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Settings))
                return false;

            Settings other = (Settings)obj;

            // Compare all the properties for equality
            return DefaultFolderSettings.Equals(other.DefaultFolderSettings) &&
                   SourceFolders.SequenceEqual(other.SourceFolders) &&
                   ExcludedFolders.SequenceEqual(other.ExcludedFolders) &&
                   LocalBackupFolder == other.LocalBackupFolder &&
                   RemoteBackupFolder == other.RemoteBackupFolder &&
                   BackupNotChangedStrategy == other.BackupNotChangedStrategy &&
                   PrefixBackupFilename == other.PrefixBackupFilename &&
                   SuffixWhenBackupNotChanged == other.SuffixWhenBackupNotChanged &&
                   PreserveFolderInArchiveStrategy == other.PreserveFolderInArchiveStrategy &&
                   BackupFileInUseStrategy == other.BackupFileInUseStrategy &&
                   RunOnce == other.RunOnce &&
                   RunInterval == other.RunInterval &&
                   DeleteBackupsWhenOlderThan == other.DeleteBackupsWhenOlderThan &&
                   MinimumBackupsToKeep == other.MinimumBackupsToKeep;
        }

        public override int GetHashCode()
        {
            HashCode hashCode = new HashCode();

            hashCode.Add(DefaultFolderSettings);
            foreach (var folder in SourceFolders)
            {
                hashCode.Add(folder);
            }
            foreach (var folder in ExcludedFolders)
            {
                hashCode.Add(folder);
            }
            hashCode.Add(LocalBackupFolder);
            hashCode.Add(RemoteBackupFolder);
            hashCode.Add(BackupNotChangedStrategy);
            hashCode.Add(PrefixBackupFilename);
            hashCode.Add(SuffixWhenBackupNotChanged);
            hashCode.Add(PreserveFolderInArchiveStrategy);
            hashCode.Add(BackupFileInUseStrategy);
            hashCode.Add(RunOnce);
            hashCode.Add(RunInterval);
            hashCode.Add(DeleteBackupsWhenOlderThan);
            hashCode.Add(MinimumBackupsToKeep);

            return hashCode.ToHashCode();
        }

        public static bool operator ==(Settings left, Settings right)
        {
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);

            return left.Equals(right);
        }

        public static bool operator !=(Settings left, Settings right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return this.AsJson();
        }
    }
}
