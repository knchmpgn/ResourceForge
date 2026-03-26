using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ResourceForge.Models;
using ResourceForge.ViewModels;

namespace ResourceForge.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public Visibility TrueValue { get; set; } = Visibility.Visible;
    public Visibility FalseValue { get; set; } = Visibility.Collapsed;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? TrueValue : FalseValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility visibility && visibility == TrueValue;
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        bool inverse = parameter is string text && text.Equals("Inverse", StringComparison.OrdinalIgnoreCase);
        bool show = inverse ? isNull : !isNull;
        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(ResourceCategory), typeof(Brush))]
public sealed class CategoryToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ResourceCategory category)
        {
            return Brushes.Gray;
        }

        var color = category switch
        {
            ResourceCategory.Icon => Color.FromRgb(0x0F, 0x76, 0xC7),
            ResourceCategory.Bitmap => Color.FromRgb(0x1A, 0x93, 0x62),
            ResourceCategory.String => Color.FromRgb(0xB0, 0x72, 0x19),
            ResourceCategory.Dialog => Color.FromRgb(0x8D, 0x4D, 0xB7),
            ResourceCategory.Version => Color.FromRgb(0x2A, 0x7F, 0xB8),
            ResourceCategory.Manifest => Color.FromRgb(0x4F, 0x8A, 0x10),
            ResourceCategory.Cursor => Color.FromRgb(0x8D, 0x56, 0x2E),
            ResourceCategory.Menu => Color.FromRgb(0x7A, 0x45, 0x8D),
            ResourceCategory.RawData => Color.FromRgb(0x5E, 0x6B, 0x7A),
            _ => Color.FromRgb(0x68, 0x68, 0x68),
        };

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(ResourceCategory), typeof(string))]
public sealed class CategoryToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ResourceCategory category ? category switch
        {
            ResourceCategory.Icon => "\uE8B9",
            ResourceCategory.Bitmap => "\uE91B",
            ResourceCategory.String => "\uE8D2",
            ResourceCategory.Dialog => "\uE8BD",
            ResourceCategory.Version => "\uE946",
            ResourceCategory.Manifest => "\uE8A5",
            ResourceCategory.Cursor => "\uE96E",
            ResourceCategory.Menu => "\uE700",
            ResourceCategory.RawData => "\uE7C3",
            _ => "\uE897",
        } : "\uE897";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(ResourceCategoryFilter), typeof(string))]
public sealed class CategoryFilterToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ResourceCategoryFilter category ? category switch
        {
            ResourceCategoryFilter.All => "\uE8FD",
            ResourceCategoryFilter.Icon => "\uE8B9",
            ResourceCategoryFilter.Bitmap => "\uE91B",
            ResourceCategoryFilter.String => "\uE8D2",
            ResourceCategoryFilter.Dialog => "\uE8BD",
            ResourceCategoryFilter.Version => "\uE946",
            ResourceCategoryFilter.Manifest => "\uE8A5",
            ResourceCategoryFilter.Cursor => "\uE96E",
            ResourceCategoryFilter.Menu => "\uE700",
            ResourceCategoryFilter.RawData => "\uE7C3",
            _ => "\uE897",
        } : "\uE897";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(ResourceCategoryFilter), typeof(string))]
public sealed class CategoryFilterToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ResourceCategoryFilter category
            ? category == ResourceCategoryFilter.RawData ? "Raw Data" : category.ToString()
            : "?";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

public sealed class IntToSuffixConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        $"{value} {parameter as string ?? string.Empty}";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class BitmapSourceToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is System.Windows.Media.Imaging.BitmapSource ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
