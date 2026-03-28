using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ResourceForge.Models;

namespace ResourceForge.Services;

/// <summary>
/// Loads, enumerates, and modifies resources inside PE binaries
/// using the Win32 resource API (kernel32.dll).
/// </summary>
public sealed class PeResourceEngine
{
    private readonly BackupService _backup;

    public PeResourceEngine(BackupService backup) => _backup = backup;

    // ══════════════════════════════════════════════════════════════════════
    //  Public API
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Enumerate all resources in the given PE file.</summary>
    public List<PeResource> LoadResources(string filePath)
    {
        var hModule = NativeMethods.LoadLibraryEx(
            filePath, IntPtr.Zero,
            NativeMethods.LOAD_LIBRARY_AS_DATAFILE | NativeMethods.LOAD_LIBRARY_AS_IMAGE_RESOURCE);

        if (hModule == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Cannot open \"{filePath}\"");

        try   { return EnumerateAll(hModule); }
        finally { NativeMethods.FreeLibrary(hModule); }
    }

    /// <summary>
    /// Replace a generic resource. A timestamped backup is created automatically.
    /// </summary>
    public void ReplaceResource(string filePath, PeResource resource, byte[] newData)
    {
        _backup.CreateBackup(filePath);

        IntPtr typePtr = NativeMethods.ResourceKeyToPtr(resource.TypeKey, out bool typeAlloc);
        IntPtr namePtr = NativeMethods.ResourceKeyToPtr(resource.NameKey, out bool nameAlloc);
        try   { WriteResource(filePath, typePtr, namePtr, resource.Language, newData); }
        finally
        {
            if (typeAlloc) Marshal.FreeHGlobal(typePtr);
            if (nameAlloc) Marshal.FreeHGlobal(namePtr);
        }
    }

    /// <summary>
    /// Replace an icon group resource (RT_GROUP_ICON + all referenced RT_ICON entries)
    /// from a source .ico file. A backup is created automatically.
    /// </summary>
    public void ReplaceIconGroup(string filePath, PeResource groupResource, string iconFilePath)
    {
        _backup.CreateBackup(filePath);

        var (groupData, iconImages, iconIds) = BuildIconGroupFromIco(iconFilePath, groupResource);

        var hUpdate = NativeMethods.BeginUpdateResource(filePath, false);
        if (hUpdate == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        bool success = false;
        try
        {
            for (int i = 0; i < iconImages.Count; i++)
            {
                WriteResourceToHandle(hUpdate,
                    new IntPtr(ResourceTypes.RT_ICON),
                    new IntPtr(iconIds[i]),
                    groupResource.Language,
                    iconImages[i]);
            }

            IntPtr namePtr = NativeMethods.ResourceKeyToPtr(groupResource.NameKey, out bool nameAlloc);
            try
            {
                WriteResourceToHandle(hUpdate,
                    new IntPtr(ResourceTypes.RT_GROUP_ICON),
                    namePtr,
                    groupResource.Language,
                    groupData);
            }
            finally { if (nameAlloc) Marshal.FreeHGlobal(namePtr); }

            success = true;
        }
        finally { NativeMethods.EndUpdateResource(hUpdate, !success); }
    }

    /// <summary>
    /// Batch-replace every RT_GROUP_ICON entry that matches the given dimensions
    /// with images from the provided .ico file. Returns count of replaced entries.
    /// </summary>
    public int BatchReplaceIcons(string filePath, List<PeResource> iconGroups,
                                  int targetWidth, int targetHeight,
                                  string iconFilePath)
    {
        _backup.CreateBackup(filePath);

        int count = 0;
        foreach (var group in iconGroups)
        {
            // Check if this group has an entry matching the requested dimensions
            var entries = ParseIconGroupEntries(group.Data);
            bool matches = entries.Any(e =>
                (e.Width  == targetWidth  || (e.Width  == 0 && targetWidth  == 256)) &&
                (e.Height == targetHeight || (e.Height == 0 && targetHeight == 256)));

            if (!matches) continue;

            var (groupData, iconImages, iconIds) = BuildIconGroupFromIco(iconFilePath, group);

            var hUpdate = NativeMethods.BeginUpdateResource(filePath, false);
            if (hUpdate == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            bool ok = false;
            try
            {
                for (int i = 0; i < iconImages.Count; i++)
                    WriteResourceToHandle(hUpdate,
                        new IntPtr(ResourceTypes.RT_ICON),
                        new IntPtr(iconIds[i]),
                        group.Language,
                        iconImages[i]);

                IntPtr namePtr = NativeMethods.ResourceKeyToPtr(group.NameKey, out bool alloc);
                try
                {
                    WriteResourceToHandle(hUpdate,
                        new IntPtr(ResourceTypes.RT_GROUP_ICON),
                        namePtr, group.Language, groupData);
                }
                finally { if (alloc) Marshal.FreeHGlobal(namePtr); }

                ok = true;
                count++;
            }
            finally { NativeMethods.EndUpdateResource(hUpdate, !ok); }
        }

        return count;
    }

    /// <summary>Delete a resource entry from the PE file. A backup is created automatically.</summary>
    public void DeleteResource(string filePath, PeResource resource)
    {
        _backup.CreateBackup(filePath);

        IntPtr typePtr = NativeMethods.ResourceKeyToPtr(resource.TypeKey, out bool typeAlloc);
        IntPtr namePtr = NativeMethods.ResourceKeyToPtr(resource.NameKey, out bool nameAlloc);
        try
        {
            var hUpdate = NativeMethods.BeginUpdateResource(filePath, false);
            if (hUpdate == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            bool ok = NativeMethods.UpdateResource(hUpdate, typePtr, namePtr,
                resource.Language, IntPtr.Zero, 0);
            NativeMethods.EndUpdateResource(hUpdate, !ok);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (typeAlloc) Marshal.FreeHGlobal(typePtr);
            if (nameAlloc) Marshal.FreeHGlobal(namePtr);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  String / Version parsing (static utilities)
    // ══════════════════════════════════════════════════════════════════════

    public static Dictionary<int, string> ParseStringResources(IEnumerable<PeResource> resources)
    {
        var result = new Dictionary<int, string>();
        foreach (var res in resources.Where(r =>
            r.TypeKey.IsInteger && r.TypeKey.IntValue == ResourceTypes.RT_STRING))
        {
            int blockId = res.NameKey.IsInteger ? res.NameKey.IntValue : 0;
            int baseId  = (blockId - 1) * 16;
            using var reader = new BinaryReader(new MemoryStream(res.Data), Encoding.Unicode);
            for (int i = 0; i < 16; i++)
            {
                if (reader.BaseStream.Position + 2 > reader.BaseStream.Length) break;
                ushort len = reader.ReadUInt16();
                if (len > 0)
                    result[baseId + i] = new string(reader.ReadChars(len));
            }
        }
        return result;
    }

    public static Dictionary<string, string> ParseVersionInfo(byte[] data)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var reader = new BinaryReader(new MemoryStream(data), Encoding.Unicode, false);

            ushort wLength      = reader.ReadUInt16();
            ushort wValueLength = reader.ReadUInt16();
            ushort wType        = reader.ReadUInt16();
            SkipNullTerminatedUnicode(reader);
            Align32(reader);

            if (wValueLength >= 52)
            {
                uint signature = reader.ReadUInt32();
                if (signature == 0xFEEF04BD)
                {
                    reader.ReadUInt32();
                    uint fileVerMS = reader.ReadUInt32();
                    uint fileVerLS = reader.ReadUInt32();
                    uint prodVerMS = reader.ReadUInt32();
                    uint prodVerLS = reader.ReadUInt32();
                    result["FileVersion"]    = $"{fileVerMS >> 16}.{fileVerMS & 0xFFFF}.{fileVerLS >> 16}.{fileVerLS & 0xFFFF}";
                    result["ProductVersion"] = $"{prodVerMS >> 16}.{prodVerMS & 0xFFFF}.{prodVerLS >> 16}.{prodVerLS & 0xFFFF}";
                    reader.ReadBytes(24); // skip remainder of FIXEDFILEINFO
                }
            }
            Align32(reader);

            while (reader.BaseStream.Position < data.Length - 4)
            {
                long   blockStart = reader.BaseStream.Position;
                ushort bLen       = reader.ReadUInt16();
                if (bLen == 0) break;

                reader.ReadUInt16(); reader.ReadUInt16();
                string key = ReadNullTerminatedUnicode(reader);
                Align32(reader);

                if (key == "StringFileInfo")
                {
                    long blockEnd = blockStart + bLen;
                    while (reader.BaseStream.Position < blockEnd - 4)
                    {
                        long   tableStart = reader.BaseStream.Position;
                        ushort tableLen   = reader.ReadUInt16();
                        if (tableLen == 0) break;
                        reader.ReadUInt16(); reader.ReadUInt16();
                        ReadNullTerminatedUnicode(reader);
                        Align32(reader);

                        long tableEnd = tableStart + tableLen;
                        while (reader.BaseStream.Position < tableEnd - 4)
                        {
                            long   strStart  = reader.BaseStream.Position;
                            ushort strLen    = reader.ReadUInt16();
                            if (strLen == 0) break;
                            ushort strValLen = reader.ReadUInt16();
                            reader.ReadUInt16();
                            string strKey = ReadNullTerminatedUnicode(reader);
                            Align32(reader);
                            string strVal = strValLen > 0
                                ? ReadNullTerminatedUnicode(reader)
                                : string.Empty;
                            Align32(reader);
                            result[strKey] = strVal;
                            reader.BaseStream.Position = strStart + strLen;
                            Align32(reader);
                        }
                        reader.BaseStream.Position = tableEnd;
                        Align32(reader);
                    }
                    break;
                }
                reader.BaseStream.Position = blockStart + bLen;
                Align32(reader);
            }
        }
        catch { /* partial parse is OK */ }
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Private — enumeration
    // ══════════════════════════════════════════════════════════════════════

    private static List<PeResource> EnumerateAll(IntPtr hModule)
    {
        var resources = new List<PeResource>();
        var typeKeys  = new List<ResourceKey>();

        NativeMethods.EnumResTypeProc typeProc = (_, type, _) =>
        {
            typeKeys.Add(ResourceKey.FromPointer(type));
            return true;
        };
        NativeMethods.EnumResourceTypesEx(hModule, typeProc, IntPtr.Zero, NativeMethods.RESOURCE_ENUM_LN, 0);

        foreach (var typeKey in typeKeys)
        {
            var   nameKeys = new List<ResourceKey>();
            IntPtr typePtr = NativeMethods.ResourceKeyToPtr(typeKey, out bool typeAlloc);
            try
            {
                NativeMethods.EnumResNameProc nameProc = (_, _, name, _) =>
                {
                    nameKeys.Add(ResourceKey.FromPointer(name));
                    return true;
                };
                NativeMethods.EnumResourceNamesEx(hModule, typePtr, nameProc, IntPtr.Zero, NativeMethods.RESOURCE_ENUM_LN, 0);
            }
            finally { if (typeAlloc) Marshal.FreeHGlobal(typePtr); }

            foreach (var nameKey in nameKeys)
            {
                typePtr = NativeMethods.ResourceKeyToPtr(typeKey, out typeAlloc);
                IntPtr namePtr = NativeMethods.ResourceKeyToPtr(nameKey, out bool nameAlloc);

                var langs = new List<ushort>();
                try
                {
                    NativeMethods.EnumResLangProc langProc = (_, _, _, lang, _) =>
                    {
                        langs.Add(lang);
                        return true;
                    };
                    NativeMethods.EnumResourceLanguagesEx(hModule, typePtr, namePtr, langProc, IntPtr.Zero, NativeMethods.RESOURCE_ENUM_LN, 0);
                }
                finally
                {
                    if (typeAlloc) Marshal.FreeHGlobal(typePtr);
                    if (nameAlloc) Marshal.FreeHGlobal(namePtr);
                }

                foreach (var lang in langs)
                {
                    var data = ReadResourceData(hModule, typeKey, nameKey, lang);
                    if (data is null) continue;

                    var category = typeKey.IsInteger
                        ? ResourceTypes.GetCategory(typeKey.IntValue)
                        : ResourceCategory.Unknown;

                    var res = new PeResource
                    {
                        TypeKey  = typeKey,
                        NameKey  = nameKey,
                        Language = lang,
                        Data     = data,
                        Category = category,
                    };
                    EnrichMetadata(res);
                    resources.Add(res);
                }
            }
        }
        return resources;
    }

    private static byte[]? ReadResourceData(IntPtr hModule, ResourceKey typeKey, ResourceKey nameKey, ushort lang)
    {
        IntPtr typePtr = NativeMethods.ResourceKeyToPtr(typeKey, out bool typeAlloc);
        IntPtr namePtr = NativeMethods.ResourceKeyToPtr(nameKey, out bool nameAlloc);
        try
        {
            IntPtr hResInfo = NativeMethods.FindResourceEx(hModule, typePtr, namePtr, lang);
            if (hResInfo == IntPtr.Zero) return null;

            uint size = NativeMethods.SizeofResource(hModule, hResInfo);
            if (size == 0) return null;

            IntPtr hResData = NativeMethods.LoadResource(hModule, hResInfo);
            if (hResData == IntPtr.Zero) return null;

            IntPtr pData = NativeMethods.LockResource(hResData);
            if (pData == IntPtr.Zero) return null;

            byte[] buffer = new byte[size];
            Marshal.Copy(pData, buffer, 0, (int)size);
            return buffer;
        }
        finally
        {
            if (typeAlloc) Marshal.FreeHGlobal(typePtr);
            if (nameAlloc) Marshal.FreeHGlobal(namePtr);
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Private — writing
    // ══════════════════════════════════════════════════════════════════════

    private static void WriteResource(string filePath, IntPtr typePtr, IntPtr namePtr, ushort lang, byte[] data)
    {
        var hUpdate = NativeMethods.BeginUpdateResource(filePath, false);
        if (hUpdate == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        bool success = false;
        try
        {
            WriteResourceToHandle(hUpdate, typePtr, namePtr, lang, data);
            success = true;
        }
        finally { NativeMethods.EndUpdateResource(hUpdate, !success); }
    }

    private static void WriteResourceToHandle(IntPtr hUpdate, IntPtr typePtr, IntPtr namePtr, ushort lang, byte[] data)
    {
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            bool ok = NativeMethods.UpdateResource(hUpdate, typePtr, namePtr, lang,
                handle.AddrOfPinnedObject(), (uint)data.Length);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error(), "UpdateResource failed");
        }
        finally { handle.Free(); }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Icon group helpers
    // ══════════════════════════════════════════════════════════════════════

    private static (byte[] groupData, List<byte[]> iconImages, List<int> iconIds)
        BuildIconGroupFromIco(string icoPath, PeResource existingGroup)
    {
        using var fs     = File.OpenRead(icoPath);
        using var reader = new BinaryReader(fs, Encoding.Unicode, false);

        reader.ReadUInt16(); // reserved
        reader.ReadUInt16(); // type
        ushort count = reader.ReadUInt16();

        var entries = new List<(byte w, byte h, byte cc, ushort planes, ushort bc, uint size, uint offset)>();
        for (int i = 0; i < count; i++)
        {
            byte   w      = reader.ReadByte();
            byte   h      = reader.ReadByte();
            byte   cc     = reader.ReadByte();
            reader.ReadByte();
            ushort planes = reader.ReadUInt16();
            ushort bc     = reader.ReadUInt16();
            uint   sz     = reader.ReadUInt32();
            uint   off    = reader.ReadUInt32();
            entries.Add((w, h, cc, planes, bc, sz, off));
        }

        var existingIds = ParseIconGroupIds(existingGroup.Data);
        var iconIds     = new List<int>();
        for (int i = 0; i < count; i++)
            iconIds.Add(i < existingIds.Count
                ? existingIds[i]
                : (existingIds.Count > 0 ? existingIds[^1] + i + 1 : i + 1));

        var iconImages = new List<byte[]>();
        foreach (var (_, _, _, _, _, sz, off) in entries)
        {
            fs.Seek(off, SeekOrigin.Begin);
            iconImages.Add(reader.ReadBytes((int)sz));
        }

        using var ms     = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.Unicode, true);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)count);

        for (int i = 0; i < count; i++)
        {
            var (w, h, cc, planes, bc, sz, _) = entries[i];
            writer.Write(w);
            writer.Write(h);
            writer.Write(cc);
            writer.Write((byte)0);
            writer.Write(planes);
            writer.Write(bc);
            writer.Write(sz);
            writer.Write((ushort)iconIds[i]);
        }

        return (ms.ToArray(), iconImages, iconIds);
    }

    public static List<IconGroupEntry> ParseIconGroupEntries(byte[] groupData)
    {
        var entries = new List<IconGroupEntry>();
        if (groupData.Length < 6) return entries;
        using var reader = new BinaryReader(new MemoryStream(groupData));
        reader.ReadUInt16(); reader.ReadUInt16();
        ushort count = reader.ReadUInt16();
        for (int i = 0; i < count; i++)
        {
            if (reader.BaseStream.Position + 14 > groupData.Length) break;
            byte   w  = reader.ReadByte();
            byte   h  = reader.ReadByte();
            byte   cc = reader.ReadByte();
            reader.ReadByte();
            ushort pl = reader.ReadUInt16();
            ushort bc = reader.ReadUInt16();
            uint   sz = reader.ReadUInt32();
            int    id = reader.ReadUInt16();
            entries.Add(new IconGroupEntry(w, h, cc, pl, bc, sz, id));
        }
        return entries;
    }

    private static List<int> ParseIconGroupIds(byte[] groupData)
        => ParseIconGroupEntries(groupData).Select(e => e.IconId).ToList();

    // ══════════════════════════════════════════════════════════════════════
    //  Metadata enrichment
    // ══════════════════════════════════════════════════════════════════════

    private static void EnrichMetadata(PeResource res)
    {
        if (!res.TypeKey.IsInteger) return;

        switch (res.TypeKey.IntValue)
        {
            case ResourceTypes.RT_GROUP_ICON when res.Data.Length >= 6:
            {
                var entries = ParseIconGroupEntries(res.Data);
                // Pick largest entry for display dimensions
                if (entries.Count > 0)
                {
                    var largest = entries.MaxBy(e => (int)e.Width * e.Height + (e.Width == 0 ? 256 * 256 : 0));
                    if (largest is not null)
                    {
                        res.Width  = largest.Width  == 0 ? 256 : largest.Width;
                        res.Height = largest.Height == 0 ? 256 : largest.Height;
                        res.BitDepth = largest.BitCount;
                    }
                }
                break;
            }

            case ResourceTypes.RT_BITMAP when res.Data.Length >= 16:
            {
                try
                {
                    using var reader = new BinaryReader(new MemoryStream(res.Data));
                    uint headerSize = reader.ReadUInt32();
                    if (headerSize >= 16)
                    {
                        res.Width    = reader.ReadInt32();
                        res.Height   = Math.Abs(reader.ReadInt32());
                        reader.ReadUInt16();
                        res.BitDepth = reader.ReadUInt16();
                    }
                }
                catch
                {
                    // Ignore bitmap parsing errors, dimensions remain 0
                }
                break;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    //  Version-info parsing helpers
    // ══════════════════════════════════════════════════════════════════════

    private static void SkipNullTerminatedUnicode(BinaryReader reader)
    {
        while (reader.BaseStream.Position + 1 < reader.BaseStream.Length)
            if (reader.ReadChar() == '\0') break;
    }

    private static string ReadNullTerminatedUnicode(BinaryReader reader)
    {
        var sb = new StringBuilder();
        while (reader.BaseStream.Position + 1 < reader.BaseStream.Length)
        {
            char c = reader.ReadChar();
            if (c == '\0') break;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static void Align32(BinaryReader reader)
    {
        long rem = reader.BaseStream.Position % 4;
        if (rem != 0) reader.BaseStream.Seek(4 - rem, SeekOrigin.Current);
    }
}
