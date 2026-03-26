using System.Text;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ResourceForge.Models;
using ResourceForge.Services;

namespace ResourceForge.ViewModels;

/// <summary>
/// Wraps a <see cref="PeResource"/> for display in the resource gallery.
/// </summary>
public sealed partial class ResourceItemViewModel : ObservableObject
{
    public PeResource Resource { get; }

    /// <summary>
    /// For RT_GROUP_ICON entries: the raw bytes of the largest referenced RT_ICON,
    /// provided by MainViewModel after correlating all loaded resources.
    /// </summary>
    public byte[]? LinkedIconData { get; set; }

    [ObservableProperty] private BitmapSource? _thumbnail;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _thumbnailLoaded;

    public ResourceItemViewModel(PeResource resource) => Resource = resource;

    partial void OnThumbnailChanged(BitmapSource? value) => OnPropertyChanged(nameof(HasThumbnail));

    public string DisplayName => Resource.NameDisplay;
    public string TypeLabel => Resource.TypeDisplay;
    public string CategoryName => Resource.Category.ToString();
    public string DataSize => Resource.DataSizeDisplay;
    public string Language => Resource.LanguageName;

    public string Dimensions => Resource.Width > 0
        ? $"{Resource.Width} x {Resource.Height}"
        : "-";

    public string BitDepthDisplay => Resource.BitDepth > 0
        ? $"{Resource.BitDepth}-bit"
        : "-";

    public bool IsVisual => Resource.IsVisual;
    public bool HasThumbnail => Thumbnail is not null;

    public string SummaryLine => $"{TypeLabel}  |  {DataSize}  |  {Language}";

    public string TooltipText => $"{TypeLabel} | {DisplayName} | {DataSize} | {Language}";

    /// <summary>Trigger thumbnail generation on first demand (async, UI-safe).</summary>
    public async Task LoadThumbnailAsync()
    {
        if (ThumbnailLoaded)
        {
            return;
        }

        ThumbnailLoaded = true;

        var thumb = await Task.Run(BuildThumbnail);
        if (thumb is not null)
        {
            Thumbnail = thumb;
        }
    }

    private BitmapSource? BuildThumbnail()
    {
        try
        {
            if (!Resource.TypeKey.IsInteger)
            {
                return null;
            }

            return Resource.TypeKey.IntValue switch
            {
                ResourceTypes.RT_ICON => ImageConversionService.IconResourceToBitmapSource(Resource.Data),
                ResourceTypes.RT_GROUP_ICON => BuildGroupIconThumbnail(),
                ResourceTypes.RT_BITMAP => ImageConversionService.BitmapResourceToBitmapSource(Resource.Data),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private BitmapSource? BuildGroupIconThumbnail()
    {
        if (LinkedIconData is null || LinkedIconData.Length == 0)
        {
            return null;
        }

        return ImageConversionService.IconResourceToBitmapSource(LinkedIconData);
    }

    public string HexPreview
    {
        get
        {
            const int maxBytes = 256;
            var data = Resource.Data;
            int count = Math.Min(data.Length, maxBytes);
            var sb = new StringBuilder(count * 3);

            for (int i = 0; i < count; i++)
            {
                if (i > 0 && i % 16 == 0)
                {
                    sb.Append('\n');
                }
                else if (i > 0 && i % 8 == 0)
                {
                    sb.Append("  ");
                }
                else if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(data[i].ToString("X2"));
            }

            if (data.Length > maxBytes)
            {
                sb.Append($"\n... ({data.Length - maxBytes} more bytes)");
            }

            return sb.ToString();
        }
    }

    public string? StringContent
    {
        get
        {
            if (!Resource.TypeKey.IsInteger || Resource.TypeKey.IntValue != ResourceTypes.RT_STRING)
            {
                return null;
            }

            var dict = PeResourceEngine.ParseStringResources(new[] { Resource });
            return dict.Count == 0
                ? "(empty block)"
                : string.Join('\n', dict.Select(kv => $"[{kv.Key,4}]  {kv.Value}"));
        }
    }

    public Dictionary<string, string>? VersionFields
    {
        get
        {
            if (!Resource.TypeKey.IsInteger || Resource.TypeKey.IntValue != ResourceTypes.RT_VERSION)
            {
                return null;
            }

            return PeResourceEngine.ParseVersionInfo(Resource.Data);
        }
    }

    public string? ManifestXml
    {
        get
        {
            if (!Resource.TypeKey.IsInteger || Resource.TypeKey.IntValue != ResourceTypes.RT_MANIFEST)
            {
                return null;
            }

            try
            {
                return DecodeText(Resource.Data);
            }
            catch
            {
                return null;
            }
        }
    }

    private static string DecodeText(byte[] data)
    {
        if (data.Length >= 2)
        {
            if (data[0] == 0xFF && data[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(data).TrimStart('\uFEFF', '\0');
            }

            if (data[0] == 0xFE && data[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(data).TrimStart('\uFEFF', '\0');
            }
        }

        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(data).TrimStart('\uFEFF', '\0');
        }

        return Encoding.UTF8.GetString(data).TrimStart('\uFEFF', '\0');
    }
}
