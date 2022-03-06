namespace Ck3IronmanWatcherService;

using System.Security.Cryptography;
using System.IO;
using Microsoft.Extensions.Hosting.Internal;

public class FileWatcherService
{
    private readonly ILogger<FileWatcherService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _applicationLifetime;

    public FileWatcherService(ILogger<FileWatcherService> logger, IConfiguration configuration, IHostApplicationLifetime applicationLifetime)
    {
        _logger = logger;
        _configuration = configuration;
        _applicationLifetime = applicationLifetime;
    }

    private string GetCrusaderKings3Directory()
    {
        var user = _configuration["User"];

        var ck3Directory = Path.Combine("C:", "Users", user, "Documents", "Paradox Interactive", "Crusader Kings III");
        return ck3Directory;
    }

    private string GetSaveGameDirectory()
    {
        var gameDirectory = GetCrusaderKings3Directory();
        var saveDirectory = Path.Combine(gameDirectory, "save games");
        return saveDirectory;
    }

    private string GetBackupDirectory()
    {
        var gameDirectory = GetCrusaderKings3Directory();
        var backupDirectory = Path.Combine(gameDirectory, "backups");
        return backupDirectory;
    }

    public void MakeBackupDirectory()
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

    private string GetPathToBackupOfSaveGame(string saveGamePath)
    {
        var backupDirectory = GetBackupDirectory();
        var fileName = Path.GetFileName(saveGamePath);
        var pathToBackupFile = Path.Combine(backupDirectory, fileName);
        return pathToBackupFile;
    }

    private string? GetPathToMostRecentBackupOfSaveGameIfExists(string saveGamePath)
    {
        var pathToBackupFile = GetPathToBackupOfSaveGame(saveGamePath);
        return File.Exists(pathToBackupFile) ? pathToBackupFile : null;
    }

    private static bool AreChecksumsEqual(string pathToFileA, string pathToFileB)
    {
        return GetChecksumOfFile(pathToFileA) == GetChecksumOfFile(pathToFileB);
    }

    private bool SaveBackupAlreadyExists(string pathToSaveGame)
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

    private async Task WaitUntilApplicationShutdown()
    {
        var waitForStop = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        _applicationLifetime.ApplicationStopping.Register(obj =>
        {
            var tcs = (TaskCompletionSource<object>)obj;
            tcs.TrySetResult(null);
        }, waitForStop);

        await waitForStop.Task.ConfigureAwait(false);
    }

    public async Task StartWatchingSaveGameDirectory()
    {
        var saveGameDirectory = GetSaveGameDirectory();
        var watcher = BuildFileSystemWatcherForDirectory(saveGameDirectory);
        _logger.LogInformation($"Watching {saveGameDirectory}.");

        await WaitUntilApplicationShutdown();
    }
}
