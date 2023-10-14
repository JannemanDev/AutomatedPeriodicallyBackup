using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

partial class Program
{
    public enum BackupNotChangedStrategy
    {
        AlwaysBackup,
        Skip,
        CreateEmptyFile,
        CreateEmptyFileWithSuffix,
    }

    public class Settings
    {
        public List<string> SourceFolders { get; init; }
        public List<string> ExcludedFolders { get; init; }
        public string LocalBackupFolder { get; init; }
        public string RemoteBackupFolder { get; init; }
        [JsonConverter(typeof(StringEnumConverter))] 
        public BackupNotChangedStrategy BackupNotChangedStrategy { get; init; }
        public string SuffixWhenBackupNotChanged { get; init; }
    }
}
