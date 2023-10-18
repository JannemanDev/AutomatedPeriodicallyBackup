using System.IO.Compression;
using K4os.Hash.xxHash;
using Newtonsoft.Json;
using System.Text;
using static Program;
using AutomatedPeriodicallyBackup;
using RegExtract;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Serilog;
using CommandLine;
using Serilog.Events;

partial class Program
{
    //Todo:
    //-return previous different backup if available -> checksum
    //-file in use

    //-suffix not start with number
    //-name backup must not end with number

    //-do not use last modified date -> just sort/use the backup nrs
    //-prefix/name of backups
    //-frequency of backups
    //-endless loop until Escape
    //-SeriLog

    static Settings settings;
    static string version;

    static void Main(string[] args)
    {
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

        string settingsFile = arguments.Value.SettingsFilename;
        string defaultSearchPattern = "*.zip";

        if (args.Length > 0)
        {
            settingsFile = args[0]; // Use specified settings file if provided
        }

        // Read settings from the specified settings file
        settings = ReadSettings(settingsFile);

        // Build the configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("settings.json") // Provide the path to your JSON file
            .Build();

        // Configure Serilog using the configuration settings
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        // Now, you can use Serilog for logging
        Log.Information("This is an information message.");

        using (ProgramInstanceChecker programInstanceChecker = new ProgramInstanceChecker(CalculateChecksum(settings.LocalBackupFolder).ToString(), CalculateChecksum(settings.RemoteBackupFolder).ToString()))
        {
            if (!programInstanceChecker.IsRunning)
            {
                // Before creating new zip backup, first rename existing backups based on their creation date
                List<string> localBackups = GetFilesFromFolders(SortDirection.FromNewestToOldest, FolderProperties.CreateFromFolder(settings.LocalBackupFolder));
                RenameAndRenumberFiles(localBackups, 1, settings.PrefixBackupFilename, settings.SuffixWhenBackupNotChanged);

                // Create a new backup 0
                string newLocalBackupFilename = Path.Combine(settings.LocalBackupFolder, "backup0.zip");
                ZipDirectories(settings.SourceFolders, newLocalBackupFilename, settings.ExcludedFolders, settings.PreserveFolderStrategy, settings.BackupFileInUseStrategy);

                string previousLocalBackupFilename = SearchPreviousCompleteBackup(FolderProperties.CreateFromFolder(settings.LocalBackupFolder), defaultSearchPattern, 1);
                CompareBackups(newLocalBackupFilename, previousLocalBackupFilename);

                // Check if RemoteBackupFolder is available
                if (!string.IsNullOrEmpty(settings.RemoteBackupFolder) && Directory.Exists(settings.RemoteBackupFolder))
                {
                    string previousRemoteBackupFilename = SearchPreviousCompleteBackup(FolderProperties.CreateFromFolder(settings.RemoteBackupFolder), defaultSearchPattern, 0);
                    string oldestLocalBackupFilename = GetFilesFromFolders(SortDirection.FromOldestToNewest, FolderProperties.CreateFromFolder(settings.LocalBackupFolder)).FirstOrDefault("");
                    CompareBackups(oldestLocalBackupFilename, previousRemoteBackupFilename);

                    // Get all zip files from both local and remote folders
                    List<string> allBackups = GetFilesFromFolders(SortDirection.FromNewestToOldest, FolderProperties.CreateFromFolder(settings.LocalBackupFolder), FolderProperties.CreateFromFolder(settings.RemoteBackupFolder));
                    RenameAndRenumberFiles(allBackups, 0, settings.PrefixBackupFilename, settings.SuffixWhenBackupNotChanged);

                    // Move all zip files from the local folder to the remote NAS folder
                    MoveFilesToFolder(GetFilesFromFolders(FolderProperties.CreateFromFolder(settings.LocalBackupFolder)), settings.RemoteBackupFolder);
                }
                else
                {
                    Log.Warning("Remote NAS folder is not available. Skipping remote operations.");
                }
            }
            else
            {
                Log.Warning($"Another instance is already running with the same zipFolderPath {settings.LocalBackupFolder} and/or remoteNASFolder {settings.RemoteBackupFolder}.");
            }
        }

        Log.CloseAndFlush();
    }

    //Will return empty string if no previous (complete) backup is found
    private static string SearchPreviousCompleteBackup(FolderProperties folder, string searchPattern, int skipFirstNumBackups = 0)
    {
        List<string> backups = GetFilesFromFolders(SortDirection.FromNewestToOldest, folder);

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
                        throw new Exception($"BackupFileInUseStrategy {settings.BackupNotChangedStrategy} not implemented!");
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

    enum SortDirection
    {
        None,
        FromOldestToNewest,
        FromNewestToOldest,
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

    static void RenameAndRenumberFiles(List<string> files, int startNr, string prefix, string suffix)
    {
        int numDigits = (int)((files.Count + 1) / 10.0) + 1; //one extra backup file and round up
        int newNr = files.Count - 1 + startNr;
        for (int i = files.Count - 1; i >= 0; i--)
        {
            string oldFullFileName = files[i];
            string oldFolder = Path.GetDirectoryName(oldFullFileName) ?? string.Empty;
            string oldFilename = Path.GetFileName(oldFullFileName);
            string oldSuffix = oldFilename.Extract<string>(@".*?\d+(.*)\.zip") ?? string.Empty;
            string newSuffix = (oldSuffix != string.Empty || FileSize(oldFullFileName) == 0) ? suffix : string.Empty;

            string formatString = $"D{numDigits}"; // Create the format string based on 'n'
            string formattedNewNr = string.Format($"{{0:{formatString}}}", newNr);

            string newFileName = Path.Combine(oldFolder, $"{prefix}{formattedNewNr}{newSuffix}.zip");

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

    static void MoveFilesToFolder(List<string> zipFiles, string newFolder)
    {
        foreach (string zipFile in zipFiles)
        {
            string remoteFilePath = Path.Combine(newFolder, Path.GetFileName(zipFile));
            File.Move(zipFile, remoteFilePath);
        }
    }

    static void ZipDirectories(List<FolderProperties> sourceDirectories, string zipPath, List<FolderProperties> excludeDirectories, PreserveFolderStrategy preserveFolderStrategy, BackupFileInUseStrategy backupFileInUseStrategy)
    {
        //get all excluded files
        List<string> excludedFullFilenames = GetFilesFromFolders(excludeDirectories).Select(f => NormalizeFilename(f)).ToList();

        using var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        foreach (var sourceDirectory in sourceDirectories)
        {
            //get all files from this directory
            List<string> fullFilenames = GetFilesFromFolders(sourceDirectory);

            //exclude fullFilenames that are listed in excludedFullFilenames
            fullFilenames = fullFilenames
                .Where(f => !excludedFullFilenames.Any(ef => ef.Equals(NormalizeFilename(f))))
                .ToList();

            AppendDirectoryToZip(fullFilenames, archive, preserveFolderStrategy, backupFileInUseStrategy, (CompressionLevel)sourceDirectory.CompressionLevel!);
        }
    }

    static long FileSize(string fileName)
    {
        return new FileInfo(fileName).Length;
    }

    static void AppendDirectoryToZip(List<string> fullFilenames, ZipArchive archive, PreserveFolderStrategy preserveFolderStrategy, BackupFileInUseStrategy backupFileInUseStrategy, CompressionLevel compressionLevel)
    {
        foreach (string fullFilename in fullFilenames)
        {
            string entryPathInZip = GetEntryPathInZip(fullFilename, preserveFolderStrategy);

            string filename = Path.GetFileName(fullFilename);
            try
            {
                archive.CreateEntryFromFile(fullFilename, $"{Path.Combine(entryPathInZip, filename)}", compressionLevel);
            }
            catch (IOException)
            {
                // The file is locked, so try to copy it and then add the copy to the ZIP archive
                switch (backupFileInUseStrategy)
                {
                    case BackupFileInUseStrategy.Skip:
                        break;
                    case BackupFileInUseStrategy.TryByMakingCopy:
                        string tempCopyPath = Path.GetTempFileName();
                        File.Copy(fullFilename, tempCopyPath, true); // Copy the file, overwriting if necessary
                        archive.CreateEntryFromFile(tempCopyPath, $"{Path.Combine(entryPathInZip, filename)}"); // Add the copy to the ZIP archive
                        File.Delete(tempCopyPath); // Clean up the temporary copy
                        break;
                    default:
                        throw new Exception($"BackupFileInUseStrategy {backupFileInUseStrategy} not implemented!");
                }

            }
        }
    }

    static string GetEntryPathInZip(string fullFilename, PreserveFolderStrategy preserveFolderStrategy)
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
        string driveLetter = root!.TrimEnd('\\').TrimEnd(':');
        string fullPath = Path.GetDirectoryName(fullFilename)!;
        string relativePath = Path.GetRelativePath(root, fullPath);
        string path = relativePath == "." ? "" : relativePath;
        string filename = Path.GetFileName(fullFilename);
        string parentPath = Path.GetFileName(Path.GetDirectoryName(fullFilename)) ?? string.Empty;

        string entryPathInZip = "";

        switch (preserveFolderStrategy)
        {
            case PreserveFolderStrategy.FullPathWithDrive:
                entryPathInZip = Path.Combine(driveLetter, relativePath);
                break;
            case PreserveFolderStrategy.FullPath:
                entryPathInZip = relativePath;
                break;
            case PreserveFolderStrategy.OnlyParentFolder:
                entryPathInZip = parentPath;
                break;
            case PreserveFolderStrategy.None:
                entryPathInZip = "";
                break;
            default:
                throw new Exception($"PreserveFolderStrategy {preserveFolderStrategy} not implemented!");
        }

        return entryPathInZip;
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
         .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Debug)
         .CreateLogger();
    }
}
