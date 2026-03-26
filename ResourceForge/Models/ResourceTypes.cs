namespace ResourceForge.Models;

/// <summary>Standard Win32 resource type identifiers (RT_*).</summary>
public static class ResourceTypes
{
    public const int RT_CURSOR       = 1;
    public const int RT_BITMAP       = 2;
    public const int RT_ICON         = 3;
    public const int RT_MENU         = 4;
    public const int RT_DIALOG       = 5;
    public const int RT_STRING       = 6;
    public const int RT_FONTDIR      = 7;
    public const int RT_FONT         = 8;
    public const int RT_ACCELERATOR  = 9;
    public const int RT_RCDATA       = 10;
    public const int RT_MESSAGETABLE = 11;
    public const int RT_GROUP_CURSOR = 12;
    public const int RT_GROUP_ICON   = 14;
    public const int RT_VERSION      = 16;
    public const int RT_DLGINCLUDE   = 17;
    public const int RT_PLUGPLAY     = 19;
    public const int RT_VXD          = 20;
    public const int RT_ANICURSOR    = 21;
    public const int RT_ANIICON      = 22;
    public const int RT_HTML         = 23;
    public const int RT_MANIFEST     = 24;

    public static ResourceCategory GetCategory(int typeId) => typeId switch
    {
        RT_ICON or RT_GROUP_ICON or RT_ANIICON         => ResourceCategory.Icon,
        RT_BITMAP                                       => ResourceCategory.Bitmap,
        RT_STRING                                       => ResourceCategory.String,
        RT_DIALOG                                       => ResourceCategory.Dialog,
        RT_VERSION                                      => ResourceCategory.Version,
        RT_MANIFEST                                     => ResourceCategory.Manifest,
        RT_CURSOR or RT_GROUP_CURSOR or RT_ANICURSOR    => ResourceCategory.Cursor,
        RT_MENU                                         => ResourceCategory.Menu,
        RT_RCDATA or RT_HTML                            => ResourceCategory.RawData,
        _                                               => ResourceCategory.Unknown,
    };

    public static string GetTypeName(int typeId) => typeId switch
    {
        RT_CURSOR       => "RT_CURSOR",
        RT_BITMAP       => "RT_BITMAP",
        RT_ICON         => "RT_ICON",
        RT_MENU         => "RT_MENU",
        RT_DIALOG       => "RT_DIALOG",
        RT_STRING       => "RT_STRING",
        RT_FONTDIR      => "RT_FONTDIR",
        RT_FONT         => "RT_FONT",
        RT_ACCELERATOR  => "RT_ACCELERATOR",
        RT_RCDATA       => "RT_RCDATA",
        RT_MESSAGETABLE => "RT_MESSAGETABLE",
        RT_GROUP_CURSOR => "RT_GROUP_CURSOR",
        RT_GROUP_ICON   => "RT_GROUP_ICON",
        RT_VERSION      => "RT_VERSION",
        RT_DLGINCLUDE   => "RT_DLGINCLUDE",
        RT_PLUGPLAY     => "RT_PLUGPLAY",
        RT_VXD          => "RT_VXD",
        RT_ANICURSOR    => "RT_ANICURSOR",
        RT_ANIICON      => "RT_ANIICON",
        RT_HTML         => "RT_HTML",
        RT_MANIFEST     => "RT_MANIFEST",
        _               => $"Type #{typeId}",
    };
}

public enum ResourceCategory
{
    Icon,
    Bitmap,
    String,
    Dialog,
    Version,
    Manifest,
    Cursor,
    Menu,
    RawData,
    Unknown,
}
