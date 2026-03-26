using System.Windows;
using Microsoft.Win32;

namespace ResourceForge;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplyTheme();

        SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category == UserPreferenceCategory.General)
            {
                Dispatcher.Invoke(ApplyTheme);
            }
        };
    }

    public static bool IsDarkMode() => IsDarkModeEnabled();

    internal static void ApplyTheme()
    {
        bool isDark = IsDarkModeEnabled();
        var resources = Current.Resources;

        if (isDark)
        {
            resources["AppBackgroundBrush"] = MakeBrush(0x10, 0x14, 0x1A);
            resources["TitleBarBrush"] = MakeBrush(0x13, 0x19, 0x22);
            resources["SidebarBackgroundBrush"] = MakeBrush(0x12, 0x18, 0x21);
            resources["CardBackgroundBrush"] = MakeBrush(0x17, 0x20, 0x2B);
            resources["CardHoverBrush"] = MakeBrush(0x1D, 0x27, 0x35);
            resources["CardSelectedBrush"] = MakeBrush(0x1B, 0x3A, 0x57);
            resources["StatusBarBrush"] = MakeBrush(0x13, 0x19, 0x22);
            resources["BorderBrush"] = MakeBrush(0x2A, 0x36, 0x45);
            resources["AccentBrush"] = MakeBrush(0x4C, 0xA8, 0xFF);
            resources["AccentHoverBrush"] = MakeBrush(0x6A, 0xB7, 0xFF);
            resources["AccentPressedBrush"] = MakeBrush(0x2B, 0x85, 0xDA);
            resources["PrimaryTextBrush"] = MakeBrush(0xF4, 0xF7, 0xFB);
            resources["SecondaryTextBrush"] = MakeBrush(0xA1, 0xAF, 0xBE);
            resources["ButtonBackgroundBrush"] = MakeBrush(0x1A, 0x24, 0x31);
            resources["ButtonHoverBrush"] = MakeBrush(0x22, 0x2F, 0x40);
            resources["InputBackgroundBrush"] = MakeBrush(0x18, 0x21, 0x2C);
            resources["DataGridRowHoverBrush"] = MakeBrush(0x1E, 0x29, 0x37);
            resources["DataGridRowSelectedBrush"] = MakeBrush(0x1B, 0x3A, 0x57);
            resources["SidebarSelectedBrush"] = MakeBrush(0x1D, 0x3E, 0x5F);
            resources["SidebarHoverBrush"] = MakeBrush(0x1C, 0x27, 0x34);
            resources["CheckerLight"] = MakeBrush(0x4A, 0x56, 0x66);
            resources["CheckerDark"] = MakeBrush(0x32, 0x3D, 0x4C);
            resources["ProgressBarBrush"] = MakeBrush(0x4C, 0xA8, 0xFF);
            resources["ScrollBarBrush"] = MakeBrush(0x56, 0x67, 0x79);
            resources["SubtleAccentBrush"] = MakeBrush(0x1E, 0x36, 0x4D);
            resources["HeroBackgroundBrush"] = MakeBrush(0x13, 0x1C, 0x28);
        }
        else
        {
            resources["AppBackgroundBrush"] = MakeBrush(0xEE, 0xF2, 0xF7);
            resources["TitleBarBrush"] = MakeBrush(0xF8, 0xFA, 0xFD);
            resources["SidebarBackgroundBrush"] = MakeBrush(0xF7, 0xF9, 0xFC);
            resources["CardBackgroundBrush"] = MakeBrush(0xFF, 0xFF, 0xFF);
            resources["CardHoverBrush"] = MakeBrush(0xF4, 0xF8, 0xFD);
            resources["CardSelectedBrush"] = MakeBrush(0xD9, 0xEB, 0xFB);
            resources["StatusBarBrush"] = MakeBrush(0xF8, 0xFA, 0xFD);
            resources["BorderBrush"] = MakeBrush(0xD9, 0xE2, 0xEC);
            resources["AccentBrush"] = MakeBrush(0x0F, 0x6C, 0xBD);
            resources["AccentHoverBrush"] = MakeBrush(0x11, 0x5E, 0xA3);
            resources["AccentPressedBrush"] = MakeBrush(0x0C, 0x3B, 0x5E);
            resources["PrimaryTextBrush"] = MakeBrush(0x16, 0x20, 0x2A);
            resources["SecondaryTextBrush"] = MakeBrush(0x5B, 0x6B, 0x7C);
            resources["ButtonBackgroundBrush"] = MakeBrush(0xFF, 0xFF, 0xFF);
            resources["ButtonHoverBrush"] = MakeBrush(0xEE, 0xF4, 0xFA);
            resources["InputBackgroundBrush"] = MakeBrush(0xFF, 0xFF, 0xFF);
            resources["DataGridRowHoverBrush"] = MakeBrush(0xF4, 0xF8, 0xFD);
            resources["DataGridRowSelectedBrush"] = MakeBrush(0xD9, 0xEB, 0xFB);
            resources["SidebarSelectedBrush"] = MakeBrush(0xDF, 0xED, 0xF9);
            resources["SidebarHoverBrush"] = MakeBrush(0xEF, 0xF5, 0xFA);
            resources["CheckerLight"] = MakeBrush(0xCD, 0xD6, 0xDF);
            resources["CheckerDark"] = MakeBrush(0xB7, 0xC3, 0xCF);
            resources["ProgressBarBrush"] = MakeBrush(0x0F, 0x6C, 0xBD);
            resources["ScrollBarBrush"] = MakeBrush(0xA8, 0xB4, 0xC0);
            resources["SubtleAccentBrush"] = MakeBrush(0xE8, 0xF2, 0xFB);
            resources["HeroBackgroundBrush"] = MakeBrush(0xF2, 0xF7, 0xFC);
        }
    }

    private static bool IsDarkModeEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0;
        }
        catch
        {
            return false;
        }
    }

    private static System.Windows.Media.SolidColorBrush MakeBrush(byte red, byte green, byte blue)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
