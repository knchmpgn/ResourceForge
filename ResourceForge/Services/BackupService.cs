using System.IO;

namespace ResourceForge.Services;

/// <summary>
/// Creates and manages timestamped backups of PE files before any modification.
/// </summary>
public sealed class BackupService
{
    /// <summary>
    /// Create a backup of <paramref name="filePath"/> in the same directory.
    /// Returns the path to the backup file.
    /// </summary>
    public string CreateBackup(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Source file not found.", filePath);

        string dir       = Path.GetDirectoryName(filePath)!;
        string baseName  = Path.GetFileNameWithoutExtension(filePath);
        string ext       = Path.GetExtension(filePath);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string backupPath = Path.Combine(dir, $"{baseName}_{timestamp}{ext}.bak");

        File.Copy(filePath, backupPath, overwrite: true);
        return backupPath;
    }

    /// <summary>Return all .bak files associated with a given PE file, newest first.</summary>
    public IReadOnlyList<BackupEntry> ListBackups(string filePath)
    {
        string dir      = Path.GetDirectoryName(filePath) ?? ".";
        string baseName = Path.GetFileNameWithoutExtension(filePath);
        string ext      = Path.GetExtension(filePath);
        string pattern  = $"{baseName}_????????_??????{ext}.bak";

        return [.. Directory.GetFiles(dir, pattern)
            .Select(p => new BackupEntry(p, new FileInfo(p).LastWriteTime, new FileInfo(p).Length))
            .OrderByDescending(e => e.Created)];
    }

    /// <summary>Restore a backup over the original file.</summary>
    public void RestoreBackup(string backupPath, string targetPath)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("Backup file not found.", backupPath);
        File.Copy(backupPath, targetPath, overwrite: true);
    }

    /// <summary>Delete a single backup.</summary>
    public void DeleteBackup(string backupPath) => File.Delete(backupPath);

    /// <summary>Delete all backups for a given file.</summary>
    public void PurgeBackups(string filePath)
    {
        foreach (var entry in ListBackups(filePath))
            File.Delete(entry.Path);
    }
}

public record BackupEntry(string Path, DateTime Created, long Size)
{
    public string SizeDisplay => Size < 1_048_576
        ? $"{Size / 1024.0:F1} KB"
        : $"{Size / 1048576.0:F2} MB";

    public string FileName => System.IO.Path.GetFileName(Path);
    public string AgeDisplay => (DateTime.Now - Created) switch
    {
        { TotalSeconds: < 60 }   ts => "Just now",
        { TotalMinutes: < 60 }   ts => $"{(int)ts.TotalMinutes}m ago",
        { TotalHours:   < 24 }   ts => $"{(int)ts.TotalHours}h ago",
        var ts                       => Created.ToString("MMM d, yyyy HH:mm"),
    };
}
