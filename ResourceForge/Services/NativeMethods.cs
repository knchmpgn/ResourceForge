using System.Runtime.InteropServices;

namespace ResourceForge.Services;

/// <summary>All Win32 P/Invoke declarations used by ResForge Studio.</summary>
internal static class NativeMethods
{
    // ── LoadLibrary flags ─────────────────────────────────────────────────
    public const uint LOAD_LIBRARY_AS_DATAFILE       = 0x00000002;
    public const uint LOAD_LIBRARY_AS_IMAGE_RESOURCE = 0x00000020;

    // ── EnumResource* flags ───────────────────────────────────────────────
    public const uint RESOURCE_ENUM_LN  = 0x0001;
    public const uint RESOURCE_ENUM_MUI = 0x0002;

    // ── DWM window attribute IDs ──────────────────────────────────────────
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE  = 20;
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_SYSTEMBACKDROP_TYPE      = 38;
    public const int DWMWCP_ROUND                   = 2;   // round corners
    public const int DWMSBT_MAINWINDOW              = 2;   // Mica

    // ── Delegates ─────────────────────────────────────────────────────────
    public delegate bool EnumResTypeProc(IntPtr hModule, IntPtr lpType, IntPtr lParam);
    public delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpType, IntPtr lpName, IntPtr lParam);
    public delegate bool EnumResLangProc(IntPtr hModule, IntPtr lpType, IntPtr lpName, ushort wLang, IntPtr lParam);

    // ── kernel32 ──────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumResourceTypesEx(
        IntPtr hModule, EnumResTypeProc lpEnumFunc,
        IntPtr lParam,  uint dwFlags, ushort LangId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumResourceNamesEx(
        IntPtr hModule, IntPtr lpType, EnumResNameProc lpEnumFunc,
        IntPtr lParam,  uint dwFlags, ushort LangId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumResourceLanguagesEx(
        IntPtr hModule, IntPtr lpType, IntPtr lpName, EnumResLangProc lpEnumFunc,
        IntPtr lParam,  uint dwFlags, ushort LangId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindResourceEx(IntPtr hModule, IntPtr lpType, IntPtr lpName, ushort wLanguage);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LockResource(IntPtr hResData);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr BeginUpdateResource(string pFileName,
        [MarshalAs(UnmanagedType.Bool)] bool bDeleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UpdateResource(
        IntPtr hUpdate, IntPtr lpType, IntPtr lpName,
        ushort wLanguage, IntPtr lpData, uint cbData);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EndUpdateResource(IntPtr hUpdate,
        [MarshalAs(UnmanagedType.Bool)] bool fDiscard);

    // ── user32 ────────────────────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CreateIconFromResourceEx(
        byte[] presbits, uint dwResSize,
        [MarshalAs(UnmanagedType.Bool)] bool fIcon,
        uint dwVer, int cxDesired, int cyDesired, uint Flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    // ── dwmapi ────────────────────────────────────────────────────────────

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── Helpers ───────────────────────────────────────────────────────────

    public static bool IS_INTRESOURCE(IntPtr value) => (ulong)(nint)value <= 0xFFFF;

    /// <summary>
    /// Convert a ResourceKey to a raw IntPtr for Win32 resource API calls.
    /// When <paramref name="allocated"/> is true the caller MUST free the pointer
    /// with <see cref="Marshal.FreeHGlobal"/> after the API call.
    /// </summary>
    public static IntPtr ResourceKeyToPtr(ResourceForge.Models.ResourceKey key, out bool allocated)
    {
        if (key.IsInteger) { allocated = false; return new IntPtr(key.IntValue); }
        allocated = true;
        return Marshal.StringToHGlobalUni(key.StringValue);
    }
}
