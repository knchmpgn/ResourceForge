using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace ResourceForge.Services;

/// <summary>
/// Converts images between formats required by the Win32 resource API.
/// Uses System.Drawing.Common (GDI+) — fully supported on Windows.
/// </summary>
public sealed class ImageConversionService
{
    private static readonly int[] DefaultIconSizes = new[] { 16, 24, 32, 48, 64, 128, 256 };

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Convert any image file (PNG, JPG, BMP, ICO) to a multi-size ICO byte array
    /// suitable for use as an RT_GROUP_ICON replacement source.
    /// </summary>
    public byte[] ConvertToIco(string sourcePath, int[]? sizes = null)
    {
        sizes ??= DefaultIconSizes;
        using var source = Image.FromFile(sourcePath);
        return BuildIco(source, sizes);
    }

    /// <summary>
    /// Convert any image file to a raw BITMAPINFO byte array (no BITMAPFILEHEADER)
    /// suitable for storing as an RT_BITMAP resource.
    /// </summary>
    public byte[] ConvertToBitmapResource(string sourcePath)
    {
        using var bmp = new Bitmap(sourcePath);
        using var ms  = new MemoryStream();
        bmp.Save(ms, ImageFormat.Bmp);
        byte[] full = ms.ToArray();
        // Strip the 14-byte BITMAPFILEHEADER
        byte[] res = new byte[full.Length - 14];
        Array.Copy(full, 14, res, 0, res.Length);
        return res;
    }

    /// <summary>
    /// Add a BITMAPFILEHEADER to raw RT_BITMAP data so it can be saved/displayed as a .bmp.
    /// </summary>
    public static byte[] BitmapResourceToFile(byte[] resourceData)
    {
        byte[] header = new byte[14];
        header[0] = (byte)'B'; header[1] = (byte)'M';
        uint fileSize = (uint)(resourceData.Length + 14);
        BitConverter.GetBytes(fileSize).CopyTo(header, 2);
        uint infoSize = resourceData.Length >= 4 ? BitConverter.ToUInt32(resourceData, 0) : 40u;
        BitConverter.GetBytes(14 + infoSize).CopyTo(header, 10);

        // Concatenate header + resourceData in a safe, allocation-minimizing way
        var result = new byte[header.Length + resourceData.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(resourceData, 0, result, header.Length, resourceData.Length);
        return result;
    }

    /// <summary>
    /// Create a WPF BitmapSource from an RT_ICON resource byte array.
    /// </summary>
    public static BitmapSource? IconResourceToBitmapSource(byte[] iconData)
    {
        try
        {
            nint hIcon = NativeMethods.CreateIconFromResourceEx(
                iconData, (uint)iconData.Length, true, 0x00030000, 0, 0, 0);
            if (hIcon == nint.Zero) return null;
            try
            {
                var image = Imaging.CreateBitmapSourceFromHIcon(
                    hIcon,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                if (image.CanFreeze)
                {
                    image.Freeze();
                }

                return image;
            }
            finally { NativeMethods.DestroyIcon(hIcon); }
        }
        catch { return null; }
    }

    /// <summary>
    /// Create a WPF BitmapSource from a raw RT_BITMAP resource byte array.
    /// </summary>
    public static BitmapSource? BitmapResourceToBitmapSource(byte[] bitmapData)
    {
        try
        {
            byte[] bmpFile = BitmapResourceToFile(bitmapData);
            using var ms   = new MemoryStream(bmpFile);
            var image      = new BitmapImage();
            image.BeginInit();
            image.StreamSource = ms;
            image.CacheOption  = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    // ── ICO construction ──────────────────────────────────────────────────

    private static byte[] BuildIco(Image source, int[] sizes)
    {
        using var ms     = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        var images = sizes.Select(sz => RenderAsPng(source, sz)).ToList();

        writer.Write((ushort)0);             // reserved
        writer.Write((ushort)1);             // type = icon
        writer.Write((ushort)sizes.Length);  // image count

        // Directory entries — placeholder offsets, computed below
        long dirOffset  = ms.Position;
        long imageStart = dirOffset + sizes.Length * 16L;

        long runningOffset = imageStart;
        for (int i = 0; i < sizes.Length; i++)
        {
            byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
            writer.Write(dim);            // width  (0 = 256)
            writer.Write(dim);            // height (0 = 256)
            writer.Write((byte)0);        // color count
            writer.Write((byte)0);        // reserved
            writer.Write((ushort)1);      // planes
            writer.Write((ushort)32);     // bit depth
            writer.Write((uint)images[i].Length);
            writer.Write((uint)runningOffset);
            runningOffset += images[i].Length;
        }

        foreach (var img in images)
            writer.Write(img);

        return ms.ToArray();
    }

    private static byte[] RenderAsPng(Image source, int size)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode  = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.SmoothingMode      = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.Clear(Color.Transparent);
            g.DrawImage(source, 0, 0, size, size);
        }
        using var output = new MemoryStream();
        bmp.Save(output, ImageFormat.Png);
        return output.ToArray();
    }
}
