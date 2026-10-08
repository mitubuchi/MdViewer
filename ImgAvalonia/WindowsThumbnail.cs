/*
    File Name : WindowsThumbnail.cs
    Programmer: Keiji Mitsubuchi

    2026.10.08  created
                Windows のサムネイル（IShellItemImageFactory）を Avalonia の Bitmap にする。
                ImgWpf.ImgRender.ThumbnailFromFile と同じ取り方で、出口だけが違う。
                Windows 以外では呼ばれない（ImgRender 側で振り分ける）。
 */
using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ImgAvalonia
{
    internal static class WindowsThumbnail
    {
        public static Bitmap Get(string fullPath, int size, bool allowIcon)
        {
            SIIGBF flags = SIIGBF.ResizeToFit;
            if (!allowIcon) flags |= SIIGBF.ThumbnailOnly;

            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                IShellItemImageFactory factory;
                SHCreateItemFromParsingName(fullPath, IntPtr.Zero,
                    typeof(IShellItemImageFactory).GUID, out factory);
                if (factory == null) return null;

                try
                {
                    var sz = new SIZE { cx = size, cy = size };
                    factory.GetImage(sz, flags, out hBitmap);
                }
                finally { Marshal.ReleaseComObject(factory); }

                if (hBitmap == IntPtr.Zero) return null;
                return FromHBitmap(hBitmap);
            }
            catch { return null; }
            finally { if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap); }
        }

        /// <summary>
        /// HBITMAP の中身を Bitmap へ写す。GetDIBits で 32bpp の BGRA として読む。
        /// Shell が返す 32bpp は乗算済み α。24bpp 以下では GetDIBits が α に 0 を書くので、
        /// そのままだと全面透明になる。その場合は α を 255 で埋める（ImgWpf の Bgr32 と同じ扱い）。
        /// </summary>
        private static Bitmap FromHBitmap(IntPtr hBitmap)
        {
            var bm = new BITMAP();
            if (GetObject(hBitmap, Marshal.SizeOf(typeof(BITMAP)), ref bm) == 0) return null;

            int w = bm.bmWidth;
            int h = bm.bmHeight;
            if (w <= 0 || h <= 0) return null;

            var bi = new BITMAPINFO();
            bi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bi.bmiHeader.biWidth = w;
            bi.bmiHeader.biHeight = -h;      // 負でトップダウン
            bi.bmiHeader.biPlanes = 1;
            bi.bmiHeader.biBitCount = 32;
            bi.bmiHeader.biCompression = BI_RGB;

            int stride = w * 4;
            var bits = new byte[stride * h];

            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero) return null;
            try
            {
                if (GetDIBits(hdc, hBitmap, 0, (uint)h, bits, ref bi, DIB_RGB_COLORS) == 0)
                    return null;
            }
            finally { ReleaseDC(IntPtr.Zero, hdc); }

            if (bm.bmBitsPixel != 32)
            {
                for (int i = 3; i < bits.Length; i += 4) bits[i] = 255;
            }

            GCHandle pin = GCHandle.Alloc(bits, GCHandleType.Pinned);
            try
            {
                return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, pin.AddrOfPinnedObject(),
                    new PixelSize(w, h), new Vector(96, 96), stride);
            }
            finally { pin.Free(); }
        }

        // ===== Win32 / COM 相互運用 =====

        [ComImport]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            void GetImage(SIZE size, SIIGBF flags, out IntPtr phbm);
        }

        [Flags]
        private enum SIIGBF
        {
            ResizeToFit = 0x00,
            BiggerSizeOk = 0x01,
            MemoryOnly = 0x02,
            IconOnly = 0x04,
            ThumbnailOnly = 0x08,
            InCacheOnly = 0x10,
            ScaleUp = 0x100,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            // GDI が書き込んでもはみ出さないよう 3 要素分だけ確保しておく
            public int c0;
            public int c1;
            public int c2;
        }

        private const uint BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr pbc,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

        [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, ref BITMAP lpvObject);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint lines,
            [Out] byte[] bits, ref BITMAPINFO bi, uint usage);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    }
}
