using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace Runly.Settings.Discovery;

/// <summary>Turns an icon source string into a bitmap at a requested edge length.
///
/// Three shapes reach this class and only the first one <see cref="Icon.ExtractAssociatedIcon"/> could
/// handle: a plain file path, a <c>"file,index"</c> pair as <see cref="IAssocHandler"/> hands it out, and
/// an <c>"@file,-id"</c> indirect string, which is what packaged applications register. The size is a
/// parameter rather than a constant because the shell keeps 16, 32, 48 and 256 pixel frames and picking
/// the frame that matches the display beats scaling a 32 pixel one up.</summary>
internal static class ShellIconLoader
{
    private const int SiigbfIconOnly = 0x04;
    private const int MaxIndirectHops = 4;

    /// <summary>Best available bitmap for <paramref name="source"/> at <paramref name="size"/> pixels, or
    /// <see langword="null"/> when every route failed. Never throws: the caller draws a placeholder.</summary>
    public static Image? Load(string source, int size)
    {
        if (string.IsNullOrWhiteSpace(source) || size <= 0)
        {
            return null;
        }

        try
        {
            return LoadCore(source, size, MaxIndirectHops);
        }
        catch (COMException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static Image? LoadCore(string source, int size, int hopsLeft)
    {
        var (path, index) = SplitIconLocation(source.Trim().Trim('"'));
        if (path.Length == 0)
        {
            return null;
        }

        // The index has to come off first: an indirect string arrives from IAssocHandler as
        // "@{package?ms-resource://...},0" and SHLoadIndirectString rejects the trailing index.
        if (path.StartsWith('@'))
        {
            if (hopsLeft <= 0)
            {
                return null;
            }

            var resolved = ResolveIndirectString(path);
            return resolved is null ? null : LoadCore(resolved, size, hopsLeft - 1);
        }

        path = Environment.ExpandEnvironmentVariables(path);

        // A package resolves its icon to a bitmap asset, not to an icon resource. Asking the shell for it
        // would return the generic icon for that file type instead of the picture itself.
        if (IsBitmapAsset(path))
        {
            return FromBitmapFile(path) ?? FromShellItem(path, size);
        }

        if (index == 0)
        {
            return FromShellItem(path, size)
                ?? FromResourceIndex(path, index, size)
                ?? FromAssociated(path);
        }

        return FromResourceIndex(path, index, size)
            ?? FromShellItem(path, size)
            ?? FromAssociated(path);
    }

    /// <summary>Splits <c>"file,index"</c>. A bare path that happens to contain a comma stays whole,
    /// because only a tail that parses as a number is treated as an index.</summary>
    private static (string Path, int Index) SplitIconLocation(string source)
    {
        var comma = source.LastIndexOf(',');
        if (comma <= 0 || comma == source.Length - 1)
        {
            return (source, 0);
        }

        var tail = source[(comma + 1)..].Trim();
        return int.TryParse(tail, System.Globalization.NumberStyles.AllowLeadingSign,
            System.Globalization.CultureInfo.InvariantCulture, out var index)
            ? (source[..comma].Trim(), index)
            : (source, 0);
    }

    private static string? ResolveIndirectString(string source)
    {
        var buffer = new StringBuilder(1024);
        return NativeMethods.SHLoadIndirectString(source, buffer, buffer.Capacity, IntPtr.Zero) == 0 &&
               buffer.Length > 0
            ? buffer.ToString()
            : null;
    }

    private static bool IsBitmapAsset(string path) =>
        Path.GetExtension(path) is ".png" or ".PNG" or ".jpg" or ".JPG" or ".jpeg" or ".bmp" or ".gif";

    /// <summary>Loads through a memory copy: <see cref="Bitmap"/> keeps the stream it was built from open
    /// for the lifetime of the image, and a file handle held on a WindowsApps asset blocks the package
    /// from updating.</summary>
    private static Image? FromBitmapFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = new MemoryStream(File.ReadAllBytes(path));
        using var source = new Bitmap(stream);
        return new Bitmap(source);
    }

    private static Image? FromShellItem(string path, int size)
    {
        var riid = NativeMethods.IidShellItemImageFactory;
        NativeMethods.IShellItemImageFactory? factory = null;
        var bitmap = IntPtr.Zero;
        try
        {
            NativeMethods.SHCreateItemFromParsingName(path, IntPtr.Zero, in riid, out factory);
            if (factory is null)
            {
                return null;
            }

            if (factory.GetImage(new NativeMethods.SIZE { cx = size, cy = size }, SiigbfIconOnly, out bitmap) != 0 ||
                bitmap == IntPtr.Zero)
            {
                return null;
            }

            return FromHBitmap(bitmap);
        }
        finally
        {
            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (factory is not null)
            {
                Marshal.ReleaseComObject(factory);
            }
        }
    }

    /// <summary>Icon at a resource index. <c>PrivateExtractIcons</c> is asked first because it is the only
    /// one of the two that honours a requested size; <c>ExtractIconEx</c> is the documented fallback and
    /// returns the system large icon.</summary>
    private static Image? FromResourceIndex(string path, int index, int size)
    {
        var handles = new IntPtr[1];
        var ids = new int[1];
        if (NativeMethods.PrivateExtractIcons(path, index, size, size, handles, ids, 1, 0) == 1 &&
            handles[0] != IntPtr.Zero)
        {
            return FromHIcon(handles[0]);
        }

        var large = new IntPtr[1];
        if (NativeMethods.ExtractIconEx(path, index, large, null!, 1) >= 1 && large[0] != IntPtr.Zero)
        {
            return FromHIcon(large[0]);
        }

        return null;
    }

    private static Image? FromAssociated(string path)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(path);
            return icon?.ToBitmap();
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Image? FromHIcon(IntPtr handle)
    {
        try
        {
            using var icon = Icon.FromHandle(handle);
            return icon.ToBitmap();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    /// <summary><c>IShellItemImageFactory</c> returns a 32 bit DIB whose alpha is premultiplied.
    /// <see cref="Image.FromHbitmap(IntPtr)"/> drops that alpha and leaves a black square around the glyph,
    /// so the bits are wrapped in the matching pixel format and copied into an owned bitmap instead.
    ///
    /// The DIB is bottom-up, which <c>GetObject</c> has no field to say: it reports a positive height for
    /// either orientation. Reading it with a positive stride mirrors every icon top to bottom, so the walk
    /// starts on the last scanline and steps backwards.</summary>
    private static Image? FromHBitmap(IntPtr handle)
    {
        var info = default(NativeMethods.BITMAP);
        if (NativeMethods.GetObject(handle, Marshal.SizeOf<NativeMethods.BITMAP>(), ref info) == 0 ||
            info.bmBits == IntPtr.Zero || info.bmBitsPixel != 32 || info.bmWidth <= 0 || info.bmHeight <= 0)
        {
            return null;
        }

        var lastScanline = info.bmBits + ((info.bmHeight - 1) * info.bmWidthBytes);
        using var wrapper = new Bitmap(info.bmWidth, info.bmHeight, -info.bmWidthBytes,
            PixelFormat.Format32bppPArgb, lastScanline);
        return new Bitmap(wrapper);
    }

    private static class NativeMethods
    {
        public static readonly Guid IidShellItemImageFactory = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, int flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            in Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int SHLoadIndirectString(
            [MarshalAs(UnmanagedType.LPWStr)] string pszSource,
            StringBuilder pszOutBuf,
            int cchOutBuf,
            IntPtr ppvReserved);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int PrivateExtractIcons(
            [MarshalAs(UnmanagedType.LPWStr)] string szFileName,
            int nIconIndex,
            int cxIcon,
            int cyIcon,
            IntPtr[] phicon,
            int[] piconid,
            int nIcons,
            int flags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int ExtractIconEx(
            [MarshalAs(UnmanagedType.LPWStr)] string lpszFile,
            int nIconIndex,
            IntPtr[] phiconLarge,
            IntPtr[]? phiconSmall,
            int nIcons);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        public static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);
    }
}
