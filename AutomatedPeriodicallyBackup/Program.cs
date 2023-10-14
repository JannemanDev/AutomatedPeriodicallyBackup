using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;
using K4os.Hash.xxHash;
using Newtonsoft.Json;
using System.Threading;
using System.Text;
//using K4os.Compression.LZ4.Legacy;

partial class Program
{
    static void Main(string[] args)
    {
        string settingsFile = "settings.json"; // Default settings file
        string defaultSearchPattern = "backup*.zip";

        if (args.Length > 0)
        {
            settingsFile = args[0]; // Use specified settings file if provided
        }

        // Read settings from the specified settings file
        Settings settings = ReadSettings(settingsFile);

        List<string> sourceDirectories = settings.SourceDirectories;
        List<string> excludeDirectories = settings.ExcludeDirectories;
        string zipFolderPath = settings.ZipFolderPath;
        string remoteNASFolder = settings.RemoteNASFolder;
        bool createEmptyFileWhenBackupNotChanged = settings.CreateEmptyFileWhenBackupNotChanged;

        // Use a named Mutex to ensure only one instance runs with the same zipFolderPath or remoteNASFolder
        bool createdNew, alreadyRunning;
        Mutex mutex1 = new Mutex(true, CalculateChecksum(zipFolderPath).ToString(), out createdNew);
        alreadyRunning = !createdNew;
        Mutex mutex2 = new Mutex(true, CalculateChecksum(remoteNASFolder).ToString(), out createdNew);
        alreadyRunning = alreadyRunning || !createdNew;

        if (alreadyRunning)
        {
            Console.WriteLine($"Another instance is already running with the same zipFolderPath {zipFolderPath} and/or remoteNASFolder {remoteNASFolder}.");
            return;
        }

        try
        {
            // Before creating new zip backup first rename existing backups based on their creation date
            RenameExistingBackups(zipFolderPath, defaultSearchPattern);

            // Create a new combined backup with the number 0
            string zipPath = Path.Combine(zipFolderPath, "backup0.zip");
            ZipDirectories(sourceDirectories, zipPath, excludeDirectories);

            // Calculate the checksum of the new backup
            ulong newChecksum = CalculateChecksumFromFile(zipPath);

            // Calculate the checksum of any existing previous backup which was different
            string backup1Path = Path.Combine(zipFolderPath, "backup1.zip");
            if (File.Exists(backup1Path))
            {
                ulong existingChecksum = CalculateChecksumFromFile(backup1Path);

                // Compare checksums and create an empty backup if they match
                if (newChecksum == existingChecksum && createEmptyFileWhenBackupNotChanged)
                {
                    // Replace backup 0 with an empty file
                    File.Create(zipPath).Dispose();
                    Console.WriteLine("Checksums match. Created an empty backup with number 0.");
                }
                else
                {
                    Console.WriteLine("Combined directories successfully zipped to " + zipPath);
                }
            }

            // Check if remoteNASFolder is available
            if (!string.IsNullOrEmpty(remoteNASFolder) && Directory.Exists(remoteNASFolder))
            {
                string remoteBackup0Path = Path.Combine(remoteNASFolder, "backup0.zip");
                if (File.Exists(remoteBackup0Path))
                {
                    // Compare the checksum of the oldest zipfile in the local folder with remote NAS backup 0
                    string oldestZipFile = GetOldestFile(zipFolderPath, defaultSearchPattern);

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
                List<string> allZipFiles = GetFilesFromFolders(defaultSearchPattern, zipFolderPath, remoteNASFolder);

                SortFilesOnDate(allZipFiles);
                RenumberFiles(allZipFiles);

                // Move all zip files from the local folder to the remote NAS folder
                MoveFilesToFolder(GetFilesFromFolders(defaultSearchPattern, zipFolderPath), remoteNASFolder);
            }
            else
            {
                Console.WriteLine("Remote NAS folder is not available. Skipping remote operations.");
            }
        }
        finally
        {
            // Release the Mutexes when done
            mutex1.ReleaseMutex();
            mutex2.ReleaseMutex();
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
        string[] backupFiles = Directory.GetFiles(folderPath, searchPattern)
            .OrderByDescending(file => GetBackupCreationDate(file))
            .ToArray();

        // Rename existing backups with numbers 1, 2, 3, etc.
        // Start with oldest
        for (int i = backupFiles.Length-1; i >= 0; i--)
        {
            string newBackupFilename = Path.Combine(folderPath, "backup" + (i + 1) + ".zip");
            string oldBackupFilename = backupFiles[i];
            File.Move(oldBackupFilename, newBackupFilename);
        }
    }

    static DateTime GetBackupCreationDate(string filePath)
    {
        return File.GetCreationTime(filePath);
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
        //rename starting with oldest
        for (int i = files.Count - 1; i >= 0; i--)
        {
            string oldFileName = files[i];
            string newFileName = Path.Combine(Path.GetDirectoryName(oldFileName), $"backup{i}.zip");
            RenameFile(oldFileName, newFileName);
        }
    }

    static void SortFilesOnDate(List<string> files)
    {
        //sort zipfiles from newest to oldest
        files.Sort((file1, file2) => File.GetLastWriteTime(file1).CompareTo(File.GetLastWriteTime(file2)));
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
}
