partial class Program
{
    public class Settings
    {
        public List<string> SourceDirectories { get; set; }
        public List<string> ExcludeDirectories { get; set; }
        public string ZipFolderPath { get; set; }
        public string RemoteNASFolder { get; set; }
        public bool CreateEmptyFileWhenBackupNotChanged { get; set; }
        public string? AddSuffixToFileWhenBackupNotChanged { get; set; }
    }
}
