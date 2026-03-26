using System.IO;
using System.Windows;
using ResourceForge.Services;

namespace ResourceForge.Dialogs;

public partial class BackupManagerWindow : Window
{
    private readonly string        _targetFile;
    private readonly BackupService _backup;

    /// <summary>True if a restore was performed — caller should reload the file.</summary>
    public bool WasRestored { get; private set; }

    public BackupManagerWindow(string targetFile, BackupService backup)
    {
        InitializeComponent();
        _targetFile = targetFile;
        _backup     = backup;

        SubtitleText.Text = System.IO.Path.GetFileName(targetFile);
        RefreshList();
    }

    private void RefreshList()
    {
        var entries = _backup.ListBackups(_targetFile);
        BackupListControl.ItemsSource = entries;

        bool hasAny = entries.Count > 0;
        EmptyText.Visibility      = hasAny ? Visibility.Collapsed : Visibility.Visible;
        PurgeAllButton.IsEnabled  = hasAny;
    }

    private void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.Tag is not BackupEntry entry) return;

        var result = MessageBox.Show(
            $"Restore backup:\n{entry.FileName}\n\nThis will overwrite the current file.",
            "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _backup.RestoreBackup(entry.Path, _targetFile);
            WasRestored = true;
            DialogResult = true; // signal MainViewModel to reload
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Restore failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        if (btn.Tag is not BackupEntry entry) return;

        var result = MessageBox.Show(
            $"Delete backup:\n{entry.FileName}?",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _backup.DeleteBackup(entry.Path);
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Delete failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PurgeAll_Click(object sender, RoutedEventArgs e)
    {
        int count = _backup.ListBackups(_targetFile).Count;
        var result = MessageBox.Show(
            $"Delete all {count} backup(s) for this file?",
            "Confirm Purge", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            _backup.PurgeBackups(_targetFile);
            RefreshList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Purge failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
