using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ResourceForge.Models;
using ResourceForge.Services;

namespace ResourceForge.Dialogs;

public partial class BatchReplaceWindow : Window
{
    private readonly string           _filePath;
    private readonly List<PeResource> _iconGroups;
    private readonly PeResourceEngine _engine;

    private string? _selectedReplacementFile;
    private int     _selectedWidth;
    private int     _selectedHeight;

    /// <summary>Count of icon groups actually replaced (available after DialogResult = true).</summary>
    public int ReplacedCount { get; private set; }

    public BatchReplaceWindow(string filePath, List<PeResource> iconGroups, PeResourceEngine engine)
    {
        InitializeComponent();
        _filePath   = filePath;
        _iconGroups = iconGroups;
        _engine     = engine;

        PopulateSizeCombo();
    }

    // ── UI Population ─────────────────────────────────────────────────────

    private void PopulateSizeCombo()
    {
        // Collect all distinct sizes present across all icon groups
        var sizes = new SortedSet<(int w, int h)>();

        foreach (var group in _iconGroups)
        {
            var entries = PeResourceEngine.ParseIconGroupEntries(group.Data);
            foreach (var e in entries)
            {
                int w = e.Width  == 0 ? 256 : e.Width;
                int h = e.Height == 0 ? 256 : e.Height;
                sizes.Add((w, h));
            }
        }

        foreach (var (w, h) in sizes)
        {
            string label = w == h ? $"{w}×{h}" : $"{w}×{h}";
            SizeCombo.Items.Add(new SizeItem(w, h, label));
        }

        if (SizeCombo.Items.Count > 0)
            SizeCombo.SelectedIndex = 0;
    }

    private void SizeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SizeCombo.SelectedItem is not SizeItem si) return;
        _selectedWidth  = si.Width;
        _selectedHeight = si.Height;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var affected = GetAffectedGroups();

        AffectedGroupsText.Text = affected.Count == 0
            ? "No icon groups contain this size."
            : $"{affected.Count} icon group(s) will be updated.";

        AffectedList.ItemsSource = affected.Count > 0
            ? affected.Select(g =>
                $"  {g.NameDisplay}  ({g.TypeDisplay})  — Lang {g.Language}")
            : ["  (none)"];

        UpdateApplyButton();
    }

    private List<PeResource> GetAffectedGroups()
    {
        return _iconGroups.Where(g =>
        {
            var entries = PeResourceEngine.ParseIconGroupEntries(g.Data);
            return entries.Any(e =>
            {
                int w = e.Width  == 0 ? 256 : e.Width;
                int h = e.Height == 0 ? 256 : e.Height;
                return w == _selectedWidth && h == _selectedHeight;
            });
        }).ToList();
    }

    private void UpdateApplyButton()
    {
        ApplyButton.IsEnabled =
            _selectedReplacementFile is not null &&
            File.Exists(_selectedReplacementFile) &&
            GetAffectedGroups().Count > 0;
    }

    // ── Browse ────────────────────────────────────────────────────────────

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select Replacement Icon",
            Filter = "Icon Files|*.ico|Image Files|*.png;*.jpg;*.bmp|All Files|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        _selectedReplacementFile = dlg.FileName;
        SelectedFileText.Text    = Path.GetFileName(dlg.FileName);
        SelectedFileText.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush");
        UpdateApplyButton();
    }

    // ── Apply ─────────────────────────────────────────────────────────────

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var affected = GetAffectedGroups();
        if (affected.Count == 0 || _selectedReplacementFile is null) return;

        var result = MessageBox.Show(
            $"Replace {_selectedWidth}×{_selectedHeight} entries in {affected.Count} icon group(s)?\n\nA backup will be created automatically.",
            "Confirm Batch Replace", MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;

            ReplacedCount = _engine.BatchReplaceIcons(
                _filePath, affected,
                _selectedWidth, _selectedHeight,
                _selectedReplacementFile);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Batch replace failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
            Mouse.OverrideCursor = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    // ── Helper record ─────────────────────────────────────────────────────

    private sealed record SizeItem(int Width, int Height, string Label)
    {
        public override string ToString() => Label;
    }
}
