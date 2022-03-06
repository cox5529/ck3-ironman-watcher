namespace Ck3IronmanWatcherService;

using System.Security.Cryptography;
using System.IO;

public class FileWatcherService
{
    private readonly ILogger<FileWatcherService> _logger;

    public FileWatcherService(ILogger<FileWatcherService> logger)
    {
        _logger = logger;
    }

    private static void ThrowExceptionIfArgumentIsNull(string? str, string argumentName)
    {
        if (string.IsNullOrEmpty(str))
        {
            throw new ArgumentNullException(argumentName, $"{argumentName} was null.");
        }
    }

    private static string GetCrusaderKings3Directory()
    {
        var userDirectory = Environment.GetEnvironmentVariable("USERPROFILE");
        ThrowExceptionIfArgumentIsNull(userDirectory, nameof(userDirectory));

        var ck3Directory = Path.Combine(userDirectory, "Documents", "Paradox Interactive", "Crusader Kings III");
        return ck3Directory;
    }

    private static string GetSaveGameDirectory()
    {
        var gameDirectory = GetCrusaderKings3Directory();
        var saveDirectory = Path.Combine(gameDirectory, "save games");
        return saveDirectory;
    }

    private static string GetBackupDirectory()
    {
        var gameDirectory = GetCrusaderKings3Directory();
        var backupDirectory = Path.Combine(gameDirectory, "backups");
        return backupDirectory;
    }

    public static void MakeBackupDirectory()
    {
        var backupDirectory = GetBackupDirectory();
        Directory.CreateDirectory(backupDirectory);
    }

    private FileSystemWatcher BuildFileSystemWatcherForDirectory(string directory)
    {
        var watcher = new FileSystemWatcher(directory)
        {
            Filter = "*.ck3",
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.Attributes
                         | NotifyFilters.CreationTime
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.FileName
                         | NotifyFilters.LastAccess
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Security
                         | NotifyFilters.Size
        };

        watcher.Changed += OnSaveGameChanged;
        watcher.Created += OnSaveGameChanged;
        return watcher;
    }

    private static string ConvertByteArrayToHexadecimal(byte[] arr)
    {
        return BitConverter.ToString(arr).Replace("-", "");
    }

    private static string GetChecksumOfFile(string pathToFile)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(pathToFile);
        return ConvertByteArrayToHexadecimal(md5.ComputeHash(stream));
    }

    private static string GetPathToBackupOfSaveGame(string saveGamePath)
    {
        var backupDirectory = GetBackupDirectory();
        var fileName = Path.GetFileName(saveGamePath);
        var pathToBackupFile = Path.Combine(backupDirectory, fileName);
        return pathToBackupFile;
    }

    private static string? GetPathToMostRecentBackupOfSaveGameIfExists(string saveGamePath)
    {
        var pathToBackupFile = GetPathToBackupOfSaveGame(saveGamePath);
        return File.Exists(pathToBackupFile) ? pathToBackupFile : null;
    }

    private static bool AreChecksumsEqual(string pathToFileA, string pathToFileB)
    {
        return GetChecksumOfFile(pathToFileA) == GetChecksumOfFile(pathToFileB);
    }

    private static bool SaveBackupAlreadyExists(string pathToSaveGame)
    {
        var backupPath = GetPathToMostRecentBackupOfSaveGameIfExists(pathToSaveGame);
        return !string.IsNullOrEmpty(backupPath) && AreChecksumsEqual(backupPath, pathToSaveGame);
    }

    private void MoveFile(string sourceFile, string destinationFile)
    {
        File.Move(sourceFile, destinationFile);
        _logger.LogInformation($"Moving {sourceFile} --> {destinationFile}");
    }

    private void DeleteFile(string filePath)
    {
        File.Delete(filePath);
        _logger.LogInformation($"Deleting {filePath}.");
    }

    private void CopyFile(string sourceFile, string destinationFile)
    {
        File.Copy(sourceFile, destinationFile);
        _logger.LogInformation($"Copying {sourceFile} --> {destinationFile}");
    }

    private static string? BuildNewFilePath(string pathToOldBackupFile)
    {
        var directory = Path.GetDirectoryName(pathToOldBackupFile);
        var filename = Path.GetFileNameWithoutExtension(pathToOldBackupFile);
        var parts = filename.Split("__");
        var ageIndicator = parts.Length == 1 ? 0 : int.Parse(parts[1]);

        if (string.IsNullOrEmpty(directory) || ageIndicator == 3)
        {
            return null;
        }

        var newFilePath = Path.Combine(directory, $"{parts[0]}__{ageIndicator + 1}.ck3");

        return newFilePath;
    }

    private void RenameOldFiles(string pathToBackupFile)
    {
        if (!File.Exists(pathToBackupFile))
        {
            return;
        }

        var newFilePath = BuildNewFilePath(pathToBackupFile);
        if (string.IsNullOrEmpty(newFilePath))
        {
            DeleteFile(pathToBackupFile);
            return;
        }

        if (File.Exists(newFilePath))
        {
            RenameOldFiles(newFilePath);
        }

        MoveFile(pathToBackupFile, newFilePath);
    }

    private void CreateSaveGameBackup(string pathToSaveFile)
    {
        var pathToBackupFile = GetPathToBackupOfSaveGame(pathToSaveFile);
        RenameOldFiles(pathToBackupFile);
        CopyFile(pathToSaveFile, pathToBackupFile);
    }

    private static bool FileHasData(string pathToFile)
    {
        var fileInfo = new FileInfo(pathToFile);
        return fileInfo.Exists && fileInfo.Length > 0;
    }

    private void OnSaveGameChanged(object sender, FileSystemEventArgs eventData)
    {
        var pathToSaveFile = eventData.FullPath;
        if (!FileHasData(pathToSaveFile))
        {
            return;
        }

        _logger.LogInformation($"Change detected on file {eventData.Name}.");
        if (SaveBackupAlreadyExists(pathToSaveFile))
        {
            _logger.LogInformation("Save has already been backed up. Ignoring change.");
            return;
        }

        CreateSaveGameBackup(pathToSaveFile);
    }

    public void StartWatchingSaveGameDirectory()
    {
        var saveGameDirectory = GetSaveGameDirectory();
        using var watcher = BuildFileSystemWatcherForDirectory(saveGameDirectory);
        _logger.LogInformation($"Watching {saveGameDirectory}. Press enter to exit.");
        Console.ReadLine();
    }
}
