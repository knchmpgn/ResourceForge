namespace ResourceForge.Models;

/// <summary>
/// Represents a single resource entry within a PE binary.
/// </summary>
public class PeResource
{
    public required ResourceKey TypeKey { get; init; }
    public required ResourceKey NameKey { get; init; }
    public ushort Language { get; init; }
    public required byte[] Data { get; init; }
    public ResourceCategory Category { get; init; }

    public int Width { get; set; }
    public int Height { get; set; }
    public int BitDepth { get; set; }

    public string TypeDisplay => TypeKey.IsInteger
        ? ResourceTypes.GetTypeName(TypeKey.IntValue)
        : (TypeKey.StringValue ?? "Unknown");

    public string NameDisplay => NameKey.IsInteger
        ? $"#{NameKey.IntValue}"
        : (NameKey.StringValue ?? "?");

    public string LanguageName => Language == 0
        ? "Neutral"
        : TryGetLanguageName(Language);

    public string DataSizeDisplay => Data.Length switch
    {
        < 1024 => $"{Data.Length} B",
        < 1024 * 1024 => $"{Data.Length / 1024.0:F1} KB",
        _ => $"{Data.Length / 1024.0 / 1024.0:F2} MB",
    };

    public bool IsVisual => Category is ResourceCategory.Icon or ResourceCategory.Bitmap or ResourceCategory.Cursor;

    private static string TryGetLanguageName(ushort lcid)
    {
        try
        {
            return System.Globalization.CultureInfo.GetCultureInfo(lcid).DisplayName;
        }
        catch
        {
            return $"LCID {lcid}";
        }
    }
}

/// <summary>
/// Represents a Win32 resource identifier, either an integer (INTRESOURCE) or a string name.
/// </summary>
public sealed class ResourceKey : IEquatable<ResourceKey>
{
    public bool IsInteger { get; }
    public int IntValue { get; }
    public string? StringValue { get; }

    public ResourceKey(int id)
    {
        IsInteger = true;
        IntValue = id;
    }

    public ResourceKey(string name)
    {
        IsInteger = false;
        StringValue = name;
    }

    public static ResourceKey FromPointer(nint ptr)
    {
        if ((ulong)ptr <= 0xFFFF)
        {
            return new ResourceKey((int)ptr);
        }

        string? name = System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr);
        return new ResourceKey(name ?? string.Empty);
    }

    public string Display => IsInteger ? IntValue.ToString() : (StringValue ?? string.Empty);

    public bool Equals(ResourceKey? other)
    {
        if (other is null || IsInteger != other.IsInteger)
        {
            return false;
        }

        return IsInteger ? IntValue == other.IntValue : StringValue == other.StringValue;
    }

    public override bool Equals(object? obj) => Equals(obj as ResourceKey);
    public override int GetHashCode() => IsInteger ? IntValue.GetHashCode() : (StringValue?.GetHashCode() ?? 0);
    public override string ToString() => Display;
}

/// <summary>Parsed GRPICONDIR entry that references an individual RT_ICON entry.</summary>
public record IconGroupEntry(
    byte Width,
    byte Height,
    byte ColorCount,
    ushort Planes,
    ushort BitCount,
    uint BytesInRes,
    int IconId);
