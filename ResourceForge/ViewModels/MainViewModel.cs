using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ResourceForge.Dialogs;
using ResourceForge.Models;
using ResourceForge.Services;

namespace ResourceForge.ViewModels;

public enum ViewMode { Grid, List }

/// <summary>
/// Main application view-model. Drives the entire UI via CommunityToolkit.Mvvm.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly PeResourceEngine _engine;
    private readonly BackupService _backup;
    private readonly ImageConversionService _converter;

    private List<ResourceItemViewModel> _allResources = new();
    private List<ResourceItemViewModel> _filteredResources = [];
    private bool _suspendRefresh;

    public MainViewModel()
    {
        _backup = new BackupService();
        _engine = new PeResourceEngine(_backup);
        _converter = new ImageConversionService();
        Categories = new ObservableCollection<ResourceCategoryFilter>(
            (ResourceCategoryFilter[])Enum.GetValues(typeof(ResourceCategoryFilter)));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile), nameof(WindowSubtitle))]
    [NotifyCanExecuteChangedFor(nameof(ReloadFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateBackupCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowBackupsCommand))]
    [NotifyCanExecuteChangedFor(nameof(BatchReplaceCommand))]
    private string? _currentFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowSubtitle))]
    private string? _fileName;

    [ObservableProperty]
    private string _statusText = "Open a PE binary to begin inspecting and editing resources.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoFilteredResults), nameof(CanInteractWithResources))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGridView), nameof(IsListView))]
    private ViewMode _currentView = ViewMode.Grid;

    [ObservableProperty]
    private int _thumbnailSize = 110;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterActive))]
    [NotifyCanExecuteChangedFor(nameof(ClearFiltersCommand))]
    private ResourceCategoryFilter _selectedCategory = ResourceCategoryFilter.All;

    partial void OnSelectedCategoryChanged(ResourceCategoryFilter value)
    {
        if (!_suspendRefresh)
        {
            RefreshFilteredResources();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterActive))]
    [NotifyCanExecuteChangedFor(nameof(ClearFiltersCommand))]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        if (!_suspendRefresh)
        {
            RefreshFilteredResources();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectedResourceSummary))]
    [NotifyCanExecuteChangedFor(nameof(ReplaceResourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportResourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteResourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(CopyResourceDataCommand))]
    private ResourceItemViewModel? _selectedResource;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResources), nameof(FilterSummary), nameof(HasNoFilteredResults))]
    private int _totalResourceCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFilteredResources), nameof(FilterSummary), nameof(HasNoFilteredResults))]
    private int _filteredResourceCount;

    partial void OnSelectedResourceChanged(ResourceItemViewModel? oldValue, ResourceItemViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
            _ = newValue.LoadThumbnailAsync();
        }
    }

    public bool HasFile => !string.IsNullOrWhiteSpace(CurrentFilePath);
    public bool HasSelection => SelectedResource is not null;
    public bool HasResources => TotalResourceCount > 0;
    public bool HasFilteredResources => FilteredResourceCount > 0;
    public bool HasNoFilteredResults => HasFile && !IsBusy && !HasFilteredResources;
    public bool CanInteractWithResources => HasFile && !IsBusy;
    public bool IsFilterActive => SelectedCategory != ResourceCategoryFilter.All || !string.IsNullOrWhiteSpace(SearchText);
    public bool IsGridView => CurrentView == ViewMode.Grid;
    public bool IsListView => CurrentView == ViewMode.List;
    public string WindowSubtitle => FileName ?? "No file open";
    public string FilterSummary => BuildFilterSummary();
    public string SelectedResourceSummary => SelectedResource is null
        ? "Select a resource to inspect details and actions."
        : $"{SelectedResource.DisplayName}  |  {SelectedResource.TypeLabel}  |  {SelectedResource.DataSize}";

    public ObservableCollection<ResourceCategoryFilter> Categories { get; }
    public IReadOnlyList<ResourceItemViewModel> FilteredResources => _filteredResources;

    [RelayCommand]
    private async Task OpenFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open PE Binary",
            Filter = "PE Binaries|*.exe;*.dll;*.mun;*.scr;*.cpl;*.ocx|All Files|*.*",
        };

        if (dlg.ShowDialog() == true)
        {
            await LoadFileAsync(dlg.FileName);
        }
    }

    [RelayCommand(CanExecute = nameof(HasFile))]
    private async Task ReloadFile()
    {
        if (CurrentFilePath is not null)
        {
            await LoadFileAsync(CurrentFilePath);
        }
    }

    [RelayCommand(CanExecute = nameof(HasFile))]
    private void CreateBackup()
    {
        try
        {
            string path = _backup.CreateBackup(CurrentFilePath!);
            StatusText = $"Backup created: {Path.GetFileName(path)}";
            MessageBox.Show($"Backup saved to:\n{path}", "Backup Created", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("Backup Failed", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasFile))]
    private void ShowBackups()
    {
        var win = new BackupManagerWindow(CurrentFilePath!, _backup)
        {
            Owner = Application.Current.MainWindow,
        };

        bool? reload = win.ShowDialog();
        if (reload == true)
        {
            _ = LoadFileAsync(CurrentFilePath!);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ReplaceResource()
    {
        if (SelectedResource is null)
        {
            return;
        }

        var resource = SelectedResource.Resource;
        bool isIconGroup = resource.TypeKey.IsInteger && resource.TypeKey.IntValue == ResourceTypes.RT_GROUP_ICON;
        bool isBitmap = resource.TypeKey.IsInteger && resource.TypeKey.IntValue == ResourceTypes.RT_BITMAP;
        bool isManifest = resource.TypeKey.IsInteger && resource.TypeKey.IntValue == ResourceTypes.RT_MANIFEST;

        string filter = isIconGroup ? "Icon Files|*.ico;*.png;*.jpg|All Files|*.*"
            : isBitmap ? "Image Files|*.bmp;*.png;*.jpg|All Files|*.*"
            : isManifest ? "XML/Manifest Files|*.xml;*.manifest|All Files|*.*"
            : "All Files|*.*";

        var dlg = new OpenFileDialog
        {
            Title = "Select Replacement File",
            Filter = filter,
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        await RunBusyAsync($"Replacing {resource.NameDisplay}...", async () =>
        {
            await Task.Run(() =>
            {
                if (isIconGroup)
                {
                    _engine.ReplaceIconGroup(CurrentFilePath!, resource, dlg.FileName);
                }
                else if (isBitmap)
                {
                    _engine.ReplaceResource(CurrentFilePath!, resource, _converter.ConvertToBitmapResource(dlg.FileName));
                }
                else
                {
                    _engine.ReplaceResource(CurrentFilePath!, resource, File.ReadAllBytes(dlg.FileName));
                }
            });

            await ReloadAndRestoreSelectionAsync(resource);
            StatusText = $"Replaced {resource.NameDisplay} from {Path.GetFileName(dlg.FileName)}.";
        });
    }

    [RelayCommand(CanExecute = nameof(HasFile))]
    private void BatchReplace()
    {
        var iconGroups = _allResources
            .Where(r => r.Resource.TypeKey.IsInteger && r.Resource.TypeKey.IntValue == ResourceTypes.RT_GROUP_ICON)
            .Select(r => r.Resource)
            .ToList();

        if (iconGroups.Count == 0)
        {
            MessageBox.Show("No icon group resources were found in this file.", "Batch Replace", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var win = new BatchReplaceWindow(CurrentFilePath!, iconGroups, _engine)
        {
            Owner = Application.Current.MainWindow,
        };

        bool? result = win.ShowDialog();
        if (result == true)
        {
            StatusText = $"Batch replace complete. {win.ReplacedCount} icon group(s) updated.";
            _ = LoadFileAsync(CurrentFilePath!);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ExportResource()
    {
        if (SelectedResource is null)
        {
            return;
        }

        var resource = SelectedResource.Resource;
        bool isIcon = resource.TypeKey.IsInteger && resource.TypeKey.IntValue == ResourceTypes.RT_ICON;
        bool isBitmap = resource.TypeKey.IsInteger && resource.TypeKey.IntValue == ResourceTypes.RT_BITMAP;
        bool isManifest = resource.TypeKey.IsInteger && resource.TypeKey.IntValue == ResourceTypes.RT_MANIFEST;

        string extension = isIcon ? "ico" : isBitmap ? "bmp" : isManifest ? "xml" : "bin";
        string defaultName = $"{Path.GetFileNameWithoutExtension(CurrentFilePath)}_{resource.NameDisplay}.{extension}";

        var dlg = new SaveFileDialog
        {
            Title = "Export Resource",
            FileName = defaultName,
            DefaultExt = extension,
            Filter = $"{extension.ToUpperInvariant()} Files|*.{extension}|All Files|*.*",
        };

        if (dlg.ShowDialog() != true)
        {
            return;
        }

        try
        {
            byte[] data = isBitmap
                ? ImageConversionService.BitmapResourceToFile(resource.Data)
                : resource.Data;

            File.WriteAllBytes(dlg.FileName, data);
            StatusText = $"Exported {resource.NameDisplay} to {Path.GetFileName(dlg.FileName)}.";
        }
        catch (Exception ex)
        {
            ShowError("Export Failed", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteResource()
    {
        if (SelectedResource is null)
        {
            return;
        }

        var resource = SelectedResource.Resource;
        var result = MessageBox.Show(
            $"Delete resource {resource.NameDisplay} ({resource.TypeDisplay})?\n\nA backup will be created automatically.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _engine.DeleteResource(CurrentFilePath!, resource);
            _allResources.Remove(SelectedResource);
            SelectedResource = null;
            TotalResourceCount = _allResources.Count;
            RefreshFilteredResources();
            StatusText = $"Deleted {resource.NameDisplay}.";
        }
        catch (Exception ex)
        {
            ShowError("Delete Failed", ex.Message);
        }
    }

    [RelayCommand]
    private void SetGridView() => CurrentView = ViewMode.Grid;

    [RelayCommand]
    private void SetListView() => CurrentView = ViewMode.List;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyResourceData()
    {
        if (SelectedResource is null)
        {
            return;
        }

        Clipboard.SetText(SelectedResource.HexPreview);
        StatusText = "Hex preview copied to clipboard.";
    }

    [RelayCommand(CanExecute = nameof(IsFilterActive))]
    private void ClearFilters()
    {
        SelectedCategory = ResourceCategoryFilter.All;
        SearchText = string.Empty;
    }

    public async Task LoadFileAsync(string path)
    {
        await RunBusyAsync($"Loading {Path.GetFileName(path)}...", async () =>
        {
            var items = await Task.Run(() =>
            {
                var resources = _engine.LoadResources(path);

                var iconDataById = resources
                    .Where(r => r.TypeKey.IsInteger && r.TypeKey.IntValue == ResourceTypes.RT_ICON && r.NameKey.IsInteger)
                    .GroupBy(r => r.NameKey.IntValue)
                    .ToDictionary(g => g.Key, g => g.First().Data);

                var vms = resources.Select(r => new ResourceItemViewModel(r)).ToList();

                foreach (var vm in vms.Where(v =>
                             v.Resource.TypeKey.IsInteger &&
                             v.Resource.TypeKey.IntValue == ResourceTypes.RT_GROUP_ICON))
                {
                    var largest = PeResourceEngine.ParseIconGroupEntries(vm.Resource.Data)
                        .OrderByDescending(entry =>
                        {
                            int width = entry.Width == 0 ? 256 : entry.Width;
                            int height = entry.Height == 0 ? 256 : entry.Height;
                            return width * height;
                        })
                        .FirstOrDefault();

                    if (largest is not null && iconDataById.TryGetValue(largest.IconId, out var iconData))
                    {
                        vm.LinkedIconData = iconData;
                    }
                }

                return vms
                    .OrderBy(vm => vm.Resource.Category)
                    .ThenBy(vm => vm.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            });

            _allResources = items;
            CurrentFilePath = path;
            FileName = Path.GetFileName(path);
            TotalResourceCount = items.Count;
            SelectedResource = null;
            _suspendRefresh = true;
            SelectedCategory = ResourceCategoryFilter.All;
            SearchText = string.Empty;
            _suspendRefresh = false;
            RefreshFilteredResources();
            StatusText = $"Loaded {items.Count} resources from {FileName}.";
            _ = PreloadThumbnailsAsync(items);
        });
    }

    public void HandleDroppedFile(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (extension is ".exe" or ".dll" or ".mun" or ".scr" or ".cpl" or ".ocx")
        {
            _ = LoadFileAsync(filePath);
        }
        else if (SelectedResource is not null && extension is ".ico" or ".png" or ".jpg" or ".bmp")
        {
            _ = ReplaceWithDroppedFile(filePath);
        }
    }

    public void SelectResource(ResourceItemViewModel? resource) => SelectedResource = resource;

    private void RefreshFilteredResources()
    {
        IEnumerable<ResourceItemViewModel> items = _allResources;

        // Apply category filter
        if (SelectedCategory != ResourceCategoryFilter.All)
        {
            var targetCategory = (ResourceCategory)(int)SelectedCategory;
            items = items.Where(resource => resource.Resource.Category == targetCategory);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string query = SearchText.Trim();
            items = items.Where(resource =>
                resource.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                resource.TypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                resource.Language.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        // Materialize the filtered results
        _filteredResources = items.ToList();
        FilteredResourceCount = _filteredResources.Count;

        // Deselect if current selection no longer matches filters
        if (SelectedResource is not null && !_filteredResources.Contains(SelectedResource))
        {
            SelectedResource = null;
        }

        // Notify UI of changes
        OnPropertyChanged(nameof(FilteredResources));
        OnPropertyChanged(nameof(FilterSummary));
        OnPropertyChanged(nameof(HasNoFilteredResults));

        // Preload thumbnails asynchronously
        _ = PreloadFilteredThumbnailsAsync();
    }

    private async Task ReplaceWithDroppedFile(string filePath)
    {
        if (SelectedResource is null)
        {
            return;
        }

        var resource = SelectedResource.Resource;

        await RunBusyAsync($"Applying {Path.GetFileName(filePath)}...", async () =>
        {
            await Task.Run(() =>
            {
                bool isIconGroup = resource.TypeKey.IsInteger && resource.TypeKey.IntValue == ResourceTypes.RT_GROUP_ICON;
                bool isBitmap = resource.TypeKey.IsInteger && resource.TypeKey.IntValue == ResourceTypes.RT_BITMAP;

                if (isIconGroup)
                {
                    _engine.ReplaceIconGroup(CurrentFilePath!, resource, filePath);
                }
                else if (isBitmap)
                {
                    _engine.ReplaceResource(CurrentFilePath!, resource, _converter.ConvertToBitmapResource(filePath));
                }
                else
                {
                    _engine.ReplaceResource(CurrentFilePath!, resource, File.ReadAllBytes(filePath));
                }
            });

            await ReloadAndRestoreSelectionAsync(resource);
            StatusText = $"Updated {resource.NameDisplay} from dropped file.";
        });
    }

    private async Task ReloadAndRestoreSelectionAsync(PeResource resource)
    {
        if (CurrentFilePath is null)
        {
            return;
        }

        var selectedCategory = SelectedCategory;
        var searchText = SearchText;

        await LoadFileAsync(CurrentFilePath);

        _suspendRefresh = true;
        SelectedCategory = selectedCategory;
        SearchText = searchText;
        _suspendRefresh = false;
        RefreshFilteredResources();

        var restored = _allResources.FirstOrDefault(candidate => ResourceMatches(candidate.Resource, resource));
        if (restored is not null)
        {
            SelectedResource = restored;
        }
    }

    private async Task PreloadThumbnailsAsync(List<ResourceItemViewModel> items)
    {
        var visualItems = items
            .Where(item => item.IsVisual || (item.Resource.TypeKey.IsInteger && item.Resource.TypeKey.IntValue == ResourceTypes.RT_GROUP_ICON))
            .Take(200)
            .ToList();

        foreach (var item in visualItems)
        {
            _ = item.LoadThumbnailAsync();
            await Task.Delay(1);
        }
    }

    private Task PreloadFilteredThumbnailsAsync() =>
        PreloadThumbnailsAsync(_filteredResources.Take(200).ToList());

    private async Task RunBusyAsync(string message, Func<Task> action)
    {
        IsBusy = true;
        BusyMessage = message;
        StatusText = message;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            if (_allResources.Count == 0)
            {
                CurrentFilePath = null;
                FileName = null;
                TotalResourceCount = 0;
                _filteredResources = [];
                FilteredResourceCount = 0;
                SelectedResource = null;
                OnPropertyChanged(nameof(FilteredResources));
            }

            ShowError("Error", ex.Message);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
            OnPropertyChanged(nameof(HasNoFilteredResults));
        }
    }

    private string BuildFilterSummary()
    {
        if (!HasFile)
        {
            return "No file loaded";
        }

        if (!IsFilterActive)
        {
            return $"{FilteredResourceCount} resources available";
        }

        string category = SelectedCategory == ResourceCategoryFilter.All ? "All categories" : SelectedCategory.ToString();
        string search = string.IsNullOrWhiteSpace(SearchText) ? "No search" : $"Search: \"{SearchText.Trim()}\"";
        return $"{FilteredResourceCount} of {TotalResourceCount} shown  |  {category}  |  {search}";
    }

    private static bool ResourceMatches(PeResource left, PeResource right) =>
        left.TypeKey.Equals(right.TypeKey) &&
        left.NameKey.Equals(right.NameKey) &&
        left.Language == right.Language;

    private static void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}

/// <summary>Maps to <see cref="ResourceCategory"/> but includes "All" sentinel.</summary>
public enum ResourceCategoryFilter
{
    All = -1,
    Icon = (int)ResourceCategory.Icon,
    Bitmap = (int)ResourceCategory.Bitmap,
    String = (int)ResourceCategory.String,
    Dialog = (int)ResourceCategory.Dialog,
    Version = (int)ResourceCategory.Version,
    Manifest = (int)ResourceCategory.Manifest,
    Cursor = (int)ResourceCategory.Cursor,
    Menu = (int)ResourceCategory.Menu,
    RawData = (int)ResourceCategory.RawData,
    Unknown = (int)ResourceCategory.Unknown,
}
