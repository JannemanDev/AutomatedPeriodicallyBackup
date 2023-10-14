using System.IO.Compression;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using K4os.Hash.xxHash;
using Newtonsoft.Json;
using System.Text;
using static Program;
using AutomatedPeriodicallyBackup;

partial class Program
{
    //Todo:
    //-return previous different backup if available -> checksum

    //-prefix/name of backups
    //-frequency of backups
    //-endless loop until Escape
    //-SeriLog

    static Settings settings;

    static void Main(string[] args)
    {
        string settingsFile = "settings.json"; // Default settings file
        string defaultSearchPattern = "backup*.zip";

        if (args.Length > 0)
        {
            settingsFile = args[0]; // Use specified settings file if provided
        }

        // Read settings from the specified settings file
        settings = ReadSettings(settingsFile);

        using (ProgramInstanceChecker programInstanceChecker = new ProgramInstanceChecker(CalculateChecksum(settings.LocalBackupFolder).ToString(), CalculateChecksum(settings.RemoteBackupFolder).ToString()))
        {
            if (!programInstanceChecker.IsRunning)
            {
                // Before creating new zip backup, first rename existing backups based on their creation date
                RenameExistingBackups(settings.LocalBackupFolder, defaultSearchPattern);

                // Create a new backup 0
                string newBackupFilename = Path.Combine(settings.LocalBackupFolder, "backup0.zip");
                ZipDirectories(settings.SourceFolders, newBackupFilename, settings.ExcludedFolders);

                // Calculate the checksum of the new backup 0
                ulong checksumNewBackup = CalculateChecksumFromFile(newBackupFilename);

                // Calculate the checksum of any existing previous backup 1 which was different
                string previousBackupFilename = Path.Combine(settings.LocalBackupFolder, "backup1.zip");
                if (File.Exists(previousBackupFilename))
                {
                    ulong checksumPreviousBackup = CalculateChecksumFromFile(previousBackupFilename);

                    // Compare checksums and create an empty backup if they match
                    if (checksumNewBackup == checksumPreviousBackup)
                    {
                        Console.WriteLine("Checksums match");

                        switch (settings.BackupNotChangedStrategy)
                        {
                            case BackupNotChangedStrategy.AlwaysBackup:
                                //do nothing and keep backup
                                break;

                            case BackupNotChangedStrategy.Skip:
                                File.Delete(newBackupFilename);
                                break;

                            case BackupNotChangedStrategy.CreateEmptyFile:
                            case BackupNotChangedStrategy.CreateEmptyFileWithSuffix:
                                CreateEmptyBackup(newBackupFilename, settings.SuffixWhenBackupNotChanged);
                                break;
                        }
                    }
                }

                // Check if RemoteBackupFolder is available
                if (!string.IsNullOrEmpty(settings.RemoteBackupFolder) && Directory.Exists(settings.RemoteBackupFolder))
                {
                    string remoteBackup0Path = Path.Combine(settings.RemoteBackupFolder, "backup0.zip");
                    if (File.Exists(remoteBackup0Path))
                    {
                        // Compare the checksum of the oldest zipfile in the local folder with remote NAS backup 0
                        string oldestZipFile = GetOldestFile(settings.LocalBackupFolder, defaultSearchPattern);

                        ulong oldestZipChecksum = CalculateChecksumFromFile(oldestZipFile);
                        ulong remoteBackup0Checksum = CalculateChecksumFromFile(remoteBackup0Path);

                        if (oldestZipChecksum == remoteBackup0Checksum)
                        {
                            // Create an empty zipfile if the checksums match
                            File.Create(oldestZipFile).Dispose();
                            Console.WriteLine("Checksums match. Created an empty backup for the oldest file.");
                        }
                    }

                    // Get all zip files from both local and remote folders
                    List<string> allZipFiles = GetFilesFromFolders(defaultSearchPattern, settings.LocalBackupFolder, settings.RemoteBackupFolder);

                    SortFilesOnDate(allZipFiles, true);
                    RenumberFiles(allZipFiles);

                    // Move all zip files from the local folder to the remote NAS folder
                    MoveFilesToFolder(GetFilesFromFolders(defaultSearchPattern, settings.LocalBackupFolder), settings.RemoteBackupFolder);
                }
                else
                {
                    Console.WriteLine("Remote NAS folder is not available. Skipping remote operations.");
                }
            }
            else
            {
                Console.WriteLine($"Another instance is already running with the same zipFolderPath {settings.LocalBackupFolder} and/or remoteNASFolder {settings.RemoteBackupFolder}.");
            }
        }
    }

    static void CreateEmptyBackup(string filename, string suffix)
    {
        File.Create(filename).Dispose();
        if (settings.BackupNotChangedStrategy == BackupNotChangedStrategy.CreateEmptyFileWithSuffix)
        {
            RenameFile(filename, AddSuffixToFilename(filename, suffix));
        }
    }

    static Settings ReadSettings(string settingsFile)
    {
        try
        {
            string json = File.ReadAllText(settingsFile);
            return JsonConvert.DeserializeObject<Settings>(json);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error reading settings: " + e.Message);
            Environment.Exit(1);
            return null; // To satisfy the compiler, although we should never reach this point
        }
    }

    static void RenameExistingBackups(string folderPath, string searchPattern)
    {
        // Get a list of existing backup files in the folder
        List<string> backupFiles = Directory.GetFiles(folderPath, searchPattern)
            .ToList();

        SortFilesOnDate(backupFiles, true);

        //renumber starting with oldest
        RenumberFiles(backupFiles);
    }

    static ulong CalculateChecksum(string input)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        return CalculateChecksum(bytes);
    }

    static ulong CalculateChecksum(byte[] bytes)
    {
        var hasher = new XXH64();
        hasher.Update(bytes);
        return hasher.Digest();
    }

    static ulong CalculateChecksumFromFile(string filePath)
    {
        using (var stream = File.OpenRead(filePath))
        {
            var hasher = new XXH64();
            byte[] buffer = new byte[8192]; // You can adjust the buffer size
            int bytesRead;
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hasher.Update(buffer, 0, bytesRead);
            }
            return hasher.Digest();
        }
    }

    static string GetOldestFile(string folderPath, string searchPattern)
    {
        string[] zipFiles = Directory.GetFiles(folderPath, searchPattern);
        if (zipFiles.Length > 0)
        {
            Array.Sort(zipFiles);
            return zipFiles[0];
        }
        return null;
    }

    static List<string> GetFilesFromFolders(string searchPattern, params string[] folderPaths)
    {
        List<string> allFiles = new List<string>();

        foreach (var folderPath in folderPaths)
        {
            allFiles.AddRange(Directory.GetFiles(folderPath, searchPattern));
        }

        return allFiles;
    }

    static void RenameFile(string oldFileName, string newFileName)
    {
        File.Move(oldFileName, newFileName);
    }

    static void RenumberFiles(List<string> files)
    {
        int newNr = files.Count;
        for (int i = files.Count - 1; i >= 0; i--)
        {
            string oldFileName = files[i];
            string oldFolder = Path.GetDirectoryName(oldFileName);
            string newFileName = Path.Combine(oldFolder, $"backup{newNr}.zip");

            RenameFile(oldFileName, newFileName);

            newNr--;
        }
    }

    static void SortFilesOnDate(List<string> files, bool sortAscending)
    {
        files.Sort((file1, file2) => sortAscending
            ? File.GetCreationTime(file1).CompareTo(File.GetCreationTime(file2))
            : File.GetCreationTime(file2).CompareTo(File.GetCreationTime(file1)));
    }

    static void MoveFilesToFolder(List<string> zipFiles, string newFolder)
    {
        foreach (string zipFile in zipFiles)
        {
            string remoteFilePath = Path.Combine(newFolder, Path.GetFileName(zipFile));
            File.Move(zipFile, remoteFilePath);
        }
    }

    static void ZipDirectories(List<string> sourceDirectories, string zipPath, List<string> excludeDirectories)
    {
        using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
        {
            var lz4Stream = LZ4Stream.Encode(zipStream, new LZ4EncoderSettings { CompressionLevel = LZ4Level.L12_MAX }, true);

            using (var archive = new ZipArchive(lz4Stream, ZipArchiveMode.Create))
            {
                foreach (var sourceDirectory in sourceDirectories)
                {
                    AppendDirectoryToZip(sourceDirectory, archive, excludeDirectories);
                }
            }
        }
    }

    static void AppendDirectoryToZip(string sourceDirectory, ZipArchive archive, List<string> excludeDirectories)
    {
        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativePath = Path.GetRelativePath(sourceDirectory, filePath);

            // Check if the file is not in an excluded directory
            bool exclude = false;
            foreach (var excludeDirectory in excludeDirectories)
            {
                if (relativePath.StartsWith(excludeDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    exclude = true;
                    break;
                }
            }

            if (!exclude)
            {
                archive.CreateEntryFromFile(filePath, relativePath);
            }
        }
    }

    static string AddSuffixToFilename(string filePath, string suffix)
    {
        string directory = Path.GetDirectoryName(filePath);
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string fileExtension = Path.GetExtension(filePath);

        // Combine the directory, filename with suffix, and extension to form the new path
        string newFilePath = Path.Combine(directory, $"{fileName}{suffix}{fileExtension}");

        return newFilePath;
    }

}
