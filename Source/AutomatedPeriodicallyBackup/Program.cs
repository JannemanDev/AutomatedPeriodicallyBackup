using AutomatedPeriodicallyBackup;
using CommandLine;
using K4os.Hash.xxHash;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RegExtract;
using Serilog;
using Serilog.Events;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

partial class Program
{
    //Todo:
    //-suffix not start with number
    //-name backup must not end with number
    //-prefix/name of backups

    static Settings? settings;
    static string version;
    static string archiveExtension;
    static string searchArchivePattern;

    static async Task Main(string[] args)
    {
        archiveExtension = ".zip";
        searchArchivePattern = $"*{archiveExtension}";

        DefaultLogging();

        var executingDir = AppContext.BaseDirectory;
        Log.Information($"Executable is running from {executingDir}");

        var compileTime = new DateTime(Builtin.CompileTime, DateTimeKind.Utc).ToLocalTime();
        version = $"Automated Periodically Backup v1.0.0 - BuildDate {compileTime}";

        Parser parser = new Parser(with =>
        {
            //with.EnableDashDash = false;
            with.HelpWriter = Console.Error;
            with.IgnoreUnknownArguments = false;
            with.CaseSensitive = false; //only applies for parameters not values assigned to them
                                        //with.ParsingCulture = CultureInfo.CurrentCulture;
        });

        ParserResult<Arguments> arguments;
        arguments = parser.ParseArguments<Arguments>(args)
            .WithParsed(RunOptions)
            .WithNotParsed(HandleParseError);

        string settingsFilename = arguments.Value.SettingsFilename;

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        do
        {
            Settings? previousSettings;
            previousSettings = settings?.Clone();
            //int? ps = previousSettings?.GetHashCode();
            //int? s = settings?.GetHashCode();
            settings = ReadSettings(settingsFilename);
            //ps = previousSettings?.GetHashCode();
            //s = settings?.GetHashCode();
            InitLogging(settingsFilename);
            

            if (!ReferenceEquals(previousSettings, null) && settings != previousSettings) Log.Information($"Settings in {settingsFilename} changed... reloading settings!");

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Prevent process termination
                cancellationTokenSource.Cancel();
            };

            PerformBackup();

            if (!settings.RunOnce)
            {
                try
                {
                    DateTime newBackupDateTime = DateTime.Now.Add(settings.RunInterval);
                    string formattedString = FormatTimeSpan(settings.RunInterval);
                    Log.Information($"Waiting, next backup will start in {formattedString} at exactly {newBackupDateTime.ToString("yyyy-MM-dd HH:mm:ss")}");
                    Log.Information($"Press <CTRL>-C to quit application");

                    int secondsToWait = (int)Math.Round(settings.RunInterval.TotalSeconds * 1000);
                    await Task.Delay(secondsToWait, cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    // Task was canceled due to Escape key press
                    Log.Information("User pressed <CTRL>-C and thereby requested to quit the application...");
                }
            }

            Log.CloseAndFlush();
        } while (!settings.RunOnce && !cancellationTokenSource.IsCancellationRequested);
    }

    public static string FormatTimeSpan(TimeSpan timeSpan)
    {
        int days = timeSpan.Days;
        int hours = timeSpan.Hours;
        int minutes = timeSpan.Minutes;
        int seconds = timeSpan.Seconds;

        List<string> parts = new List<string>();

        if (days > 0)
        {
            parts.Add($"{days} {(days == 1 ? "day" : "days")}");
        }

        if (hours > 0)
        {
            parts.Add($"{hours} {(hours == 1 ? "hour" : "hours")}");
        }

        if (minutes > 0)
        {
            parts.Add($"{minutes} {(minutes == 1 ? "minute" : "minutes")}");
        }

        if (seconds > 0)
        {
            parts.Add($"{seconds} {(seconds == 1 ? "second" : "seconds")}");
        }

        if (parts.Count > 1)
        {
            string lastPart = parts[parts.Count - 1];
            parts[parts.Count - 1] = "and " + lastPart;
        }

        return string.Join(parts.Count > 2 ? ", " : " ", parts);
    }

    private static void InitLogging(string settingsFile)
    {
        // Build the configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(settingsFile) // Provide the path to your JSON file
            .Build();

        // Configure Serilog using the configuration settings
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }

    private static void PerformBackup()
    {
        using (ProgramInstanceChecker programInstanceChecker = new ProgramInstanceChecker(CalculateChecksum(settings.LocalBackupFolder).ToString(), CalculateChecksum(settings.RemoteBackupFolder).ToString()))
        {
            if (!programInstanceChecker.IsRunning)
            {
                if (!string.IsNullOrEmpty(settings.LocalBackupFolder) && Directory.Exists(settings.LocalBackupFolder))
                {
                    // Before creating new archive backup, first rename existing backups based on their creation date
                    List<string> localBackups = GetFilesFromFolder(SortDirection.FromNewestToOldest, FolderProperties.CreateFromFolder(settings.LocalBackupFolder));
                    RenumberFiles(localBackups, 1, settings.PrefixBackupFilename, settings.SuffixWhenBackupNotChanged);

                    // Create a new backup 0
                    int numDigits = NrOfDigits(localBackups.Count + 1);
                    string zeros = FormatNumberWithLeadingZeros(0, numDigits);
                    string newLocalBackupFilename = Path.Combine(settings.LocalBackupFolder, $"{settings.PrefixBackupFilename}{zeros}{archiveExtension}");
                    ArchiveDirectories(newLocalBackupFilename, settings.SourceFolders, settings.ExcludedFolders, settings.PreserveFolderInArchiveStrategy, settings.BackupFileInUseStrategy);

                    string previousCompleteLocalBackupFilename = SearchPreviousCompleteBackup(FolderProperties.CreateFromFolder(settings.LocalBackupFolder), searchArchivePattern, 1);
                    CompareBackups(newLocalBackupFilename, previousCompleteLocalBackupFilename);

                    // Check if RemoteBackupFolder is available
                    if (!string.IsNullOrEmpty(settings.RemoteBackupFolder) && Directory.Exists(settings.RemoteBackupFolder))
                    {
                        string latestCompleteRemoteBackupFilename = SearchPreviousCompleteBackup(FolderProperties.CreateFromFolder(settings.RemoteBackupFolder), searchArchivePattern, 0);
                        string oldestLocalBackupFilename = GetFilesFromFolder(SortDirection.FromOldestToNewest, FolderProperties.CreateFromFolder(settings.LocalBackupFolder)).FirstOrDefault("");
                        CompareBackups(oldestLocalBackupFilename, latestCompleteRemoteBackupFilename);

                        // Get all archive files from both local and remote folders
                        List<string> allBackups = GetFilesFromFolders(SortDirection.FromNewestToOldest, FolderProperties.CreateFromFolder(settings.LocalBackupFolder), FolderProperties.CreateFromFolder(settings.RemoteBackupFolder));
                        RenumberFiles(allBackups, 0, settings.PrefixBackupFilename, settings.SuffixWhenBackupNotChanged);

                        // Move all archive files from the local folder to the remote folder
                        MoveFilesToFolder(GetFilesFromFolders(FolderProperties.CreateFromFolder(settings.LocalBackupFolder)), settings.RemoteBackupFolder);

                        List<string> allRemoteBackups = GetFilesFromFolders(SortDirection.FromNewestToOldest, FolderProperties.CreateFromFolder(settings.RemoteBackupFolder));
                        
                        // Delete old backups
                        List<BackupInfo> tooOldRemoteBackups = allRemoteBackups
                                   .Skip(settings.MinimumBackupsToKeep)
                                   .Select(backup => new BackupInfo
                                   {
                                       Filename = backup,
                                       Age = FileAgeSinceLastModification(backup)
                                   })
                                   .Where(info => info.Age > settings.DeleteBackupsWhenOlderThan)
                                   .ToList();

                        if (tooOldRemoteBackups.Any())
                        {
                            Log.Information($"Found {tooOldRemoteBackups.Count} backup(s) which are older than {FormatTimeSpan(settings.DeleteBackupsWhenOlderThan)}:");

                            tooOldRemoteBackups.ForEach(f =>
                            {
                                TimeSpan delta = f.Age - settings.DeleteBackupsWhenOlderThan;
                                Log.Information($" Deleting an too old backup which is {FormatTimeSpan(delta)} too old:");
                                Log.Information($"  \"{f.Filename}\" is {FormatTimeSpan(f.Age)} old");

                                File.Delete(f.Filename);
                            });
                        }
                    }
                    else
                    {
                        Log.Warning($"RemoteBackupFolder \"{settings.RemoteBackupFolder}\" is currently not available. Skipping remote operations.");
                    }
                }
                else
                {
                    Log.Warning($"LocalBackupFolder \"{settings.LocalBackupFolder}\" is currently not available. Skipping this backup round.");
                }
            }
            else
            {
                Log.Error($"Another instance is already running with the same LocalBackupFolder \"{settings.LocalBackupFolder}\" and/or RemoteBackupFolder \"{settings.RemoteBackupFolder}\".");
            }
        }
    }

    //Will return empty string if no previous (complete) backup is found
    private static string SearchPreviousCompleteBackup(FolderProperties folder, string searchPattern, int skipFirstNumBackups = 0)
    {
        List<string> backups = GetFilesFromFolder(SortDirection.FromNewestToOldest, folder);

        return backups
            .Skip(skipFirstNumBackups)
            .Where(b => FileSize(b) > 0)
            .FirstOrDefault("");
    }

    private static void CompareBackups(string newBackupFilename, string previousBackupFilename)
    {
        // Calculate the checksum of the new backup 0
        ulong checksumNewBackup = CalculateChecksumFromFile(newBackupFilename);

        // Calculate the checksum of any existing previous backup 1 which was different
        if (File.Exists(previousBackupFilename))
        {
            ulong checksumPreviousBackup = CalculateChecksumFromFile(previousBackupFilename);

            // Compare checksums and create an empty backup if they match
            if (checksumNewBackup == checksumPreviousBackup)
            {
                Log.Information($"Checksums match of:");
                Log.Information($" {newBackupFilename}");
                Log.Information($" {previousBackupFilename}");

                switch (settings.BackupNotChangedStrategy)
                {
                    case BackupNotChangedStrategy.Keep:
                        //do nothing and keep backup
                        break;

                    case BackupNotChangedStrategy.Delete:
                        File.Delete(newBackupFilename);
                        break;

                    case BackupNotChangedStrategy.CreateEmptyBackup:
                    case BackupNotChangedStrategy.CreateEmptyBackupWithSuffix:
                        CreateEmptyBackup(newBackupFilename, settings.SuffixWhenBackupNotChanged);
                        break;
                    default:
                        throw new Exception($"BackupFileInUseStrategy {settings.BackupNotChangedStrategy} not implemented in {MethodBase.GetCurrentMethod().Name}!");
                }
            }
        }
    }

    static void CreateEmptyBackup(string filename, string suffix)
    {
        // Get the original last modified date
        DateTime originalLastModified = File.GetLastWriteTime(filename);
        File.Create(filename).Dispose();

        if (settings.BackupNotChangedStrategy == BackupNotChangedStrategy.CreateEmptyBackupWithSuffix)
        {
            RenameFile(filename, AddSuffixToFilename(filename, suffix));
        }

        // Restore the original last modified date to the empty backup file
        File.SetLastWriteTime(filename, originalLastModified);
    }

    static Settings ReadSettings(string settingsFile)
    {
        try
        {
            string json = File.ReadAllText(settingsFile);
            return JsonConvert.DeserializeObject<Settings>(json);
        }
        catch (JsonSerializationException ex)
        {
            Log.Error(GetEnumErrorDescription(ex));
        }
        catch (Exception e)
        {
            Log.Error("Error reading settings:\n" + e.Message);
        }

        Environment.Exit(1);
        return null; // To satisfy the compiler, although we should never reach this point
    }

    private static string GetEnumErrorDescription(JsonSerializationException ex)
    {
        string propertyName = ex.Path;
        Type enumType = GetPropertyEnumType(propertyName);
        if (enumType != null)
        {
            string enumValues = string.Join(", ", Enum.GetNames(enumType));
            return $"Error reading JSON settings:\n{ex.Message}\nPossible values for {propertyName}: {enumValues}";
        }

        return $"Error reading settings:\n{ex.Message}";
    }

    private static Type GetPropertyEnumType(string propertyName)
    {
        Type settingsType = typeof(Settings);
        var propertyInfo = settingsType.GetProperty(propertyName);
        if (propertyInfo != null && propertyInfo.PropertyType.IsEnum)
        {
            return propertyInfo.PropertyType;
        }
        return null;
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

    static List<string> GetFilesFromFolder(FolderProperties folderPath)
    {
        return GetFilesFromFolder(SortDirection.None, folderPath);
    }

    static List<string> GetFilesFromFolder(SortDirection sortDirection, FolderProperties folderPath)
    {
        SearchOption searchOption = (bool)folderPath.IncludeSubFolders! ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        List<string> allFiles = Directory.GetFiles(folderPath.Folder, folderPath.FilePattern!, searchOption)
            .Where(f =>
            {
                long fileSize = FileSize(f);
                return fileSize >= folderPath.MinFileSize && fileSize <= folderPath.MaxFileSize;
            })
            .ToList();

        if (sortDirection != SortDirection.None) SortFiles(allFiles, sortDirection);

        return allFiles;
    }

    static List<string> GetFilesFromFolders(params FolderProperties[] folderPaths)
    {
        return GetFilesFromFolders(SortDirection.None, folderPaths.ToList());
    }

    static List<string> GetFilesFromFolders(List<FolderProperties> folderPaths)
    {
        return GetFilesFromFolders(SortDirection.None, folderPaths);
    }

    //Returns files from each folder sorted and as one big list (in same order as folderPaths)
    static List<string> GetFilesFromFolders(SortDirection sortDirection, params FolderProperties[] folderPaths)
    {
        return GetFilesFromFolders(sortDirection, folderPaths.ToList());
    }

    //Returns files from each folder sorted and as one big list (in same order as folderPaths)
    static List<string> GetFilesFromFolders(SortDirection sortDirection, List<FolderProperties> folderPaths)
    {
        List<string> allFiles = new List<string>();

        foreach (FolderProperties folderPath in folderPaths)
        {
            allFiles.AddRange(GetFilesFromFolder(sortDirection, folderPath));
        }

        return allFiles;
    }

    static void RenameFile(string oldFileName, string newFileName)
    {
        File.Move(oldFileName, newFileName);
    }

    static int NrOfDigits(int i)
    {
        return i.ToString().Length;
    }

    static string FormatNumberWithLeadingZeros(int n, int numDigits)
    {
        string formatString = $"D{numDigits}"; // Create the format string based on 'n'
        string formattedNewNr = string.Format($"{{0:{formatString}}}", n);

        return formattedNewNr;
    }

    static void RenumberFiles(List<string> files, int startNr, string prefix, string suffix)
    {
        int numDigits = NrOfDigits(files.Count + 1); //one extra backup file and round up
        int newNr = files.Count - 1 + startNr;
        for (int i = files.Count - 1; i >= 0; i--)
        {
            string oldFullFileName = files[i];
            string oldFolder = Path.GetDirectoryName(oldFullFileName) ?? string.Empty;
            string oldFilename = Path.GetFileName(oldFullFileName);
            string oldSuffix = oldFilename.Extract<string>(@$".*?\d+(.*){Regex.Escape(archiveExtension)}") ?? string.Empty;
            string newSuffix = (oldSuffix != string.Empty || FileSize(oldFullFileName) == 0) ? suffix : string.Empty;

            string formattedNewNr = FormatNumberWithLeadingZeros(newNr, numDigits);

            string newFileName = Path.Combine(oldFolder, $"{prefix}{formattedNewNr}{newSuffix}{archiveExtension}");

            RenameFile(oldFullFileName, newFileName);

            newNr--;
        }
    }

    static void SortFiles(List<string> files, SortDirection sortDirection)
    {
        files.Sort((file1, file2) => (sortDirection == SortDirection.FromNewestToOldest)
            ? file1.CompareTo(file2)
            : file2.CompareTo(file1));
    }

    static void MoveFilesToFolder(List<string> archiveFiles, string newFolder)
    {
        foreach (string archiveFile in archiveFiles)
        {
            string remoteFilePath = Path.Combine(newFolder, Path.GetFileName(archiveFile));
            File.Move(archiveFile, remoteFilePath);
        }
    }

    static void ArchiveDirectories(string archivePath, List<FolderProperties> sourceDirectories, List<FolderProperties> excludedDirectories, PreserveFolderInArchiveStrategy preserveFolderStrategy, BackupFileInUseStrategy backupFileInUseStrategy)
    {
        //get all excluded files
        List<string> excludedFullFilenames = GetFilesFromFolders(excludedDirectories).Select(f => NormalizeFilename(f)).ToList();

        using var archiveStream = new FileStream(archivePath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create);

        foreach (var sourceDirectory in sourceDirectories)
        {
            //get all files from this directory
            List<string> fullFilenames = GetFilesFromFolder(sourceDirectory);

            //exclude fullFilenames that are listed in excludedFullFilenames
            fullFilenames = fullFilenames
                .Where(f => !excludedFullFilenames.Any(ef => ef.Equals(NormalizeFilename(f))))
                .ToList();

            AppendDirectoryToArchive(fullFilenames, archive, preserveFolderStrategy, backupFileInUseStrategy, (CompressionLevel)sourceDirectory.CompressionLevel!);
        }
    }

    static TimeSpan FileAgeSinceLastModification(string filename)
    {
        FileInfo fileInfo = new FileInfo(filename);

        // Calculate file age based on last modification time
        TimeSpan fileAgeBasedOnModification = DateTime.Now - fileInfo.LastWriteTime;

        return fileAgeBasedOnModification;
    }

    static long FileSize(string fileName)
    {
        return new FileInfo(fileName).Length;
    }

    static void AppendDirectoryToArchive(List<string> fullFilenames, ZipArchive archive, PreserveFolderInArchiveStrategy preserveFolderStrategy, BackupFileInUseStrategy backupFileInUseStrategy, CompressionLevel compressionLevel)
    {
        foreach (string fullFilename in fullFilenames)
        {
            string entryPathInArchive = GetEntryPathInArchive(fullFilename, preserveFolderStrategy);

            string filename = Path.GetFileName(fullFilename);
            try
            {
                archive.CreateEntryFromFile(fullFilename, $"{Path.Combine(entryPathInArchive, filename)}", compressionLevel);
            }
            catch (IOException)
            {
                // The file is locked, so try to copy it and then add the copy to the archive
                switch (backupFileInUseStrategy)
                {
                    case BackupFileInUseStrategy.Skip:
                        break;
                    case BackupFileInUseStrategy.TryByMakingCopy:
                        string tempCopyPath = Path.GetTempFileName();
                        File.Copy(fullFilename, tempCopyPath, true); // Copy the file, overwriting if necessary
                        archive.CreateEntryFromFile(tempCopyPath, $"{Path.Combine(entryPathInArchive, filename)}"); // Add the copy to the archive
                        File.Delete(tempCopyPath); // Clean up the temporary copy
                        break;
                    default:
                        throw new Exception($"BackupFileInUseStrategy {backupFileInUseStrategy} not implemented in {MethodBase.GetCurrentMethod().Name}!");
                }

            }
        }
    }

    static string GetEntryPathInArchive(string fullFilename, PreserveFolderInArchiveStrategy preserveFolderInArchiveStrategy)
    {
        //Testcases:
        //fullFilename = @"c:\temp\test1\file.txt";
        //fullFilename = @"c:\temp\file.txt";
        //fullFilename = @"c:\file.txt";

        //For example: c:\temp\test1\file.txt
        //FullPathWithDrive,  // /c/temp/test1/file.txt
        //FullPath,           // /temp/test1/file.txt
        //OnlyParentFolder,   // /test1/file.txt
        //None,               // /file.txt

        string root = Path.GetPathRoot(fullFilename)!;
        string fullPath = Path.GetDirectoryName(fullFilename)!;
        string relativePath = Path.GetRelativePath(root, fullPath);

        string entryPathInArchive;

        switch (preserveFolderInArchiveStrategy)
        {
            case PreserveFolderInArchiveStrategy.FullPathWithDrive:
                string driveLetter = root!.TrimEnd('\\').TrimEnd(':');
                entryPathInArchive = Path.Combine(driveLetter, relativePath);
                break;
            case PreserveFolderInArchiveStrategy.FullPath:
                entryPathInArchive = relativePath;
                break;
            case PreserveFolderInArchiveStrategy.OnlyParentFolder:
                string parentPath = Path.GetFileName(Path.GetDirectoryName(fullFilename)) ?? string.Empty;
                entryPathInArchive = parentPath;
                break;
            case PreserveFolderInArchiveStrategy.None:
                entryPathInArchive = "";
                break;
            default:
                throw new Exception($"PreserveFolderStrategy {preserveFolderInArchiveStrategy} not implemented in {MethodBase.GetCurrentMethod().Name}!");
        }

        return entryPathInArchive;
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

    static string NormalizeFilename(string filename)
    {
        //since we do not use relative paths check command below
        //filename = Path.GetFullPath(filename);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            //Windows is not case sensitive
            return filename.ToLower();
        }
        else
        {
            return filename;
        }
    }

    static void RunOptions(Arguments opts)
    {
        LogInfoHeader(version, opts.AsJson());

        //do some extra checks
        if (!File.Exists(opts.SettingsFilename))
        {
            Log.Error($"Settings file {opts.SettingsFilename} NOT found!");
            Environment.Exit(1);
        }
    }

    static void LogInfoHeader(string version, string arguments)
    {
        Log.Information($"{version}");
        Log.Information("Argument values used (when argument was not given default value is used):");
        Log.Information(arguments);
    }

    static void HandleParseError(IEnumerable<Error> errs)
    {
        Environment.Exit(1);
    }

    static void DefaultLogging()
    {
        //Create default minimal logger until settings are loaded
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose() //send all events to sinks
            .WriteTo.Console(
                outputTemplate: "{Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Debug)
            .CreateLogger();
    }
}
