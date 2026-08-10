/*
    File Name : ImgRender.cs
    Programmer: Keiji Mitsubuchi

    2026.08.10  created
                画像ファイルを WPF の BitmapSource に変換する。
                自前のデコーダを持たない形式（動画など）は Windows の
                サムネイル（IShellItemImageFactory）で代替する。
                SvgRender と同じく、失敗時は例外を投げず null を返す。

    ■ Usage:
        image.Source = ImgRender.From(@"C:\data\photo.jpg", 600);
        image.Source = ImgRender.ThumbnailFromFile(@"C:\data\clip.mp4", 256);
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImgWpf
{
    public static class ImgRender
    {
        /// <summary>サムネイルの既定の一辺（px）</summary>
        public static int ThumbnailSize = 256;

        // WIC で読める形式。webp / heic / avif は OS のコーデック次第で、
        // 読めなければサムネイル側へ回る
        private static readonly HashSet<string> ImageExt =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".jfif", ".gif", ".bmp", ".dib",
            ".tif", ".tiff", ".ico", ".wdp", ".jxr",
            ".webp", ".heic", ".heif", ".avif",
        };

        // デコーダを持たないのでサムネイルで表示する形式
        private static readonly HashSet<string> VideoExt =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".mkv", ".webm",
            ".mpg", ".mpeg", ".m2ts", ".ts", ".flv", ".3gp", ".asf", ".ogv",
        };

        /// <summary>画像ファイルの拡張子か</summary>
        public static bool IsImageFile(string path)
        {
            return ImageExt.Contains(ExtOf(path));
        }

        /// <summary>動画ファイルの拡張子か</summary>
        public static bool IsVideoFile(string path)
        {
            return VideoExt.Contains(ExtOf(path));
        }

        /// <summary>ImgWpf が表示を試みる拡張子か</summary>
        public static bool IsSupported(string path)
        {
            string e = ExtOf(path);
            return ImageExt.Contains(e) || VideoExt.Contains(e);
        }

        private static string ExtOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            try { return Path.GetExtension(path); }
            catch { return ""; }
        }

        /// <summary>
        /// 中身に応じて自動で振り分ける。失敗したら null。
        /// 画像はデコードし、動画やデコードできない形式はサムネイルで代替する。
        /// maxWidth &gt; 0 なら、それより大きい画像はデコード時に縮小する。
        ///
        /// 種類アイコンでの代替はしない。壊れた画像が白紙アイコンとして
        /// 表示されるより、null を返して呼び出し側に判断させたほうがよい。
        /// </summary>
        public static BitmapSource From(string path, int maxWidth = 0)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            if (!IsVideoFile(path))
            {
                BitmapSource src = FromFile(path, maxWidth);
                if (src != null) return src;
                // ここに来るのは webp / heic などでコーデックが無い場合
            }
            return ThumbnailFromFile(path, maxWidth > 0 ? maxWidth : ThumbnailSize);
        }

        /// <summary>
        /// 画像ファイルを WIC でデコードする。失敗したら null。
        /// decodeWidth &gt; 0 かつ原寸がそれより大きい場合は、デコード時に縮小して
        /// メモリと時間を節約する（読み込んでから縮小するのではない）。
        /// 原寸より大きい値を渡しても拡大はしない。
        /// </summary>
        public static BitmapSource FromFile(string path, int decodeWidth = 0)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                // 縮小が要るかどうかはヘッダの原寸で決める
                int dw = 0;
                if (decodeWidth > 0)
                {
                    int src = PixelWidthOf(path);
                    if (src > decodeWidth) dw = decodeWidth;
                }

                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return Decode(fs, dw);
            }
            catch { return null; }
        }

        /// <summary>Stream からデコードする。失敗したら null</summary>
        public static BitmapSource FromStream(Stream stream, int decodeWidth = 0)
        {
            if (stream == null) return null;
            try { return Decode(stream, decodeWidth); }
            catch { return null; }
        }

        /// <summary>
        /// Uri（file:// / http:// / pack://）からデコードする。失敗したら null。
        /// 原寸が分からないため、こちらは縮小指定を受け付けない
        /// （表示側の MaxWidth で合わせること）。
        /// </summary>
        public static BitmapSource FromUri(Uri uri)
        {
            if (uri == null) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                if (bmp.CanFreeze) bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        private static BitmapSource Decode(Stream s, int decodeWidth)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = s;
            // OnLoad にしないと EndInit 後に Stream を閉じられない
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
            bmp.EndInit();
            if (bmp.CanFreeze) bmp.Freeze();
            return bmp;
        }

        /// <summary>画像の原寸の横幅（px）。ヘッダだけ読む。取れなければ 0</summary>
        public static int PixelWidthOf(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var dec = BitmapDecoder.Create(fs,
                        BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                        BitmapCacheOption.None);
                    if (dec.Frames.Count == 0) return 0;
                    return dec.Frames[0].PixelWidth;
                }
            }
            catch { return 0; }
        }

        // ===== Windows のサムネイル =====

        /// <summary>
        /// Windows のサムネイルを取得する。動画のポスターフレームもこれで取れる。
        /// サムネイルはエクスプローラと同じものが返り、OS のサムネイルキャッシュに
        /// 乗るため 2 回目以降は速い。取れなければ null。
        ///
        /// allowIcon に true を渡すと、サムネイルを持たないファイルでは
        /// 種類アイコン（白紙アイコンなど）で代替する。既定は false。
        /// 中身の絵が欲しい用途で true にすると、壊れた画像がアイコンとして
        /// 表示されてしまい失敗に気づけないので注意。
        ///
        /// COM を使うので STA スレッドから呼ぶこと（WPF の UI スレッドは STA）。
        /// 初回生成は動画だと数百 ms かかることがある。
        /// </summary>
        public static BitmapSource ThumbnailFromFile(string path, int size = 0, bool allowIcon = false)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (size <= 0) size = ThumbnailSize;

            SIIGBF flags = SIIGBF.ResizeToFit;
            if (!allowIcon) flags |= SIIGBF.ThumbnailOnly;

            IntPtr hBitmap = IntPtr.Zero;
            try
            {
                IShellItemImageFactory factory;
                SHCreateItemFromParsingName(Path.GetFullPath(path), IntPtr.Zero,
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
        /// HBITMAP の中身を BitmapSource へ写す。
        /// Imaging.CreateBitmapSourceFromHBitmap は α を落として透明部分が黒くなるため、
        /// GetDIBits で自分で読む。
        /// </summary>
        private static BitmapSource FromHBitmap(IntPtr hBitmap)
        {
            var bm = new BITMAP();
            if (GetObject(hBitmap, Marshal.SizeOf(typeof(BITMAP)), ref bm) == 0) return null;

            int w = bm.bmWidth;
            int h = bm.bmHeight;
            if (w <= 0 || h <= 0) return null;

            // Shell が返す 32bpp は乗算済み α なので Pbgra32。
            // 24bpp 以下は GetDIBits が α に 0 を書くため、α を見ない Bgr32 にする
            // （そのままだと全面透明になってしまう）
            PixelFormat fmt = bm.bmBitsPixel == 32 ? PixelFormats.Pbgra32 : PixelFormats.Bgr32;

            var bi = new BITMAPINFO();
            bi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bi.bmiHeader.biWidth = w;
            bi.bmiHeader.biHeight = -h;      // 負でトップダウン（WPF と同じ並び）
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

            BitmapSource src = BitmapSource.Create(w, h, 96, 96, fmt, null, bits, stride);
            src.Freeze();   // UI スレッド外でも使えるようにする
            return src;
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
            // BI_RGB の 32bpp ではパレットを使わないが、GDI が書き込んでも
            // はみ出さないよう 3 要素分だけ確保しておく
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
