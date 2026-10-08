/*
    File Name : ImgRender.cs
    Programmer: Keiji Mitsubuchi

    2026.10.08  created
                画像ファイルを Avalonia の Bitmap に変換する（ImgWpf.ImgRender の Avalonia 版）。
                デコードは SkiaSharp、EXIF の向きも SkiaSharp が読んだ値を当てる。
                自前のデコーダを持たない形式（動画など）は Windows のサムネイル
                （IShellItemImageFactory）で代替する。Windows 以外では null を返す。
                ImgWpf と同じく、失敗時は例外を投げず null を返す。

    ■ Usage:
        image.Source = ImgRender.From(@"C:\data\photo.jpg", 600);
        image.Source = ImgRender.ThumbnailFromFile(@"C:\data\clip.mp4", 256);
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace ImgAvalonia
{
    public static class ImgRender
    {
        /// <summary>サムネイルの既定の一辺（px）</summary>
        public static int ThumbnailSize = 256;

        // 拡張子の一覧は ImgWpf と同じにしておく（ホストから見た「扱える種類」を揃えるため）。
        // SkiaSharp が読めない形式（tif / heic など）は、Windows ではサムネイル側へ回る
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

        /// <summary>ImgAvalonia が表示を試みる拡張子か</summary>
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
        public static Bitmap From(string path, int maxWidth = 0)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

            if (!IsVideoFile(path))
            {
                Bitmap src = FromFile(path, maxWidth);
                if (src != null) return src;
                // ここに来るのは tif / heic など SkiaSharp が読めない形式
            }
            return ThumbnailFromFile(path, maxWidth > 0 ? maxWidth : ThumbnailSize);
        }

        /// <summary>
        /// 画像ファイルをデコードする。失敗したら null。
        /// decodeWidth &gt; 0 かつ原寸がそれより大きい場合は縮小する。
        /// 原寸より大きい値を渡しても拡大はしない。
        /// </summary>
        public static Bitmap FromFile(string path, int decodeWidth = 0)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    return Decode(fs, decodeWidth);
            }
            catch { return null; }
        }

        /// <summary>Stream からデコードする。失敗したら null</summary>
        public static Bitmap FromStream(Stream stream, int decodeWidth = 0)
        {
            if (stream == null) return null;
            try { return Decode(stream, decodeWidth); }
            catch { return null; }
        }

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        /// <summary>
        /// Uri（file:// / http:// / https://）から読む。失敗したら null。
        /// http は取りに行くので時間がかかる。UI スレッドからは呼ばず、
        /// 作業スレッドで呼んでから結果を UI に渡すこと（Bitmap はどのスレッドで作ってもよい）。
        /// </summary>
        public static Bitmap FromUri(Uri uri, int decodeWidth = 0)
        {
            if (uri == null) return null;
            try
            {
                if (uri.IsFile) return From(uri.LocalPath, decodeWidth);
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

                byte[] data = Http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
                using (var ms = new MemoryStream(data))
                    return Decode(ms, decodeWidth);
            }
            catch { return null; }
        }

        private static Bitmap Decode(Stream s, int decodeWidth)
        {
            // SKCodec はシークできるストリームを要る。ファイル以外は一度メモリへ写す
            Stream src = s;
            if (!s.CanSeek)
            {
                var ms = new MemoryStream();
                s.CopyTo(ms);
                ms.Position = 0;
                src = ms;
            }

            using (var managed = new SKManagedStream(src, false))
            using (var codec = SKCodec.Create(managed))
            {
                if (codec == null) return null;

                // GIF などは最初のコマだけを描く（WPF の BitmapImage と同じ）
                var info = new SKImageInfo(codec.Info.Width, codec.Info.Height,
                    SKColorType.Bgra8888, SKAlphaType.Premul);
                using (var full = new SKBitmap(info))
                {
                    SKCodecResult r = codec.GetPixels(info, full.GetPixels());
                    if (r != SKCodecResult.Success && r != SKCodecResult.IncompleteInput) return null;

                    using (SKBitmap oriented = Orient(full, codec.EncodedOrigin))
                    {
                        SKBitmap use = oriented ?? full;
                        using (SKBitmap scaled = Shrink(use, decodeWidth))
                            return ToBitmap(scaled ?? use);
                    }
                }
            }
        }

        /// <summary>幅が decodeWidth を超えるときだけ縮小したものを返す。不要なら null</summary>
        private static SKBitmap Shrink(SKBitmap src, int decodeWidth)
        {
            if (decodeWidth <= 0 || src.Width <= decodeWidth) return null;

            int h = (int)Math.Max(1, Math.Round(src.Height * (double)decodeWidth / src.Width));
            var info = new SKImageInfo(decodeWidth, h, SKColorType.Bgra8888, SKAlphaType.Premul);
            return src.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
        }

        // ===== EXIF の向き =====

        /// <summary>
        /// EXIF の Orientation を絵そのものに当てる。当てる必要が無ければ null。
        ///
        /// 携帯で撮った縦写真は横向きのまま記録され「あとで 90 度回して見せる」ことに
        /// なっている。エクスプローラーや OS のサムネイルは当てて見せるため、
        /// 当てないと同じファイルが場所によって違う向きで出る（ImgWpf と同じ理由）。
        /// </summary>
        private static SKBitmap Orient(SKBitmap src, SKEncodedOrigin origin)
        {
            if (origin == SKEncodedOrigin.TopLeft) return null;

            bool swap = origin == SKEncodedOrigin.LeftTop || origin == SKEncodedOrigin.RightTop ||
                        origin == SKEncodedOrigin.RightBottom || origin == SKEncodedOrigin.LeftBottom;
            int w = swap ? src.Height : src.Width;
            int h = swap ? src.Width : src.Height;

            var dst = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(dst))
            {
                switch (origin)
                {
                    case SKEncodedOrigin.TopRight:      // 2: 左右反転
                        canvas.Scale(-1, 1, w / 2f, 0);
                        break;
                    case SKEncodedOrigin.BottomRight:   // 3: 180 度
                        canvas.RotateDegrees(180, w / 2f, h / 2f);
                        break;
                    case SKEncodedOrigin.BottomLeft:    // 4: 上下反転
                        canvas.Scale(1, -1, 0, h / 2f);
                        break;
                    case SKEncodedOrigin.LeftTop:       // 5: 左右反転して 90 度
                        canvas.Translate(w, 0);
                        canvas.RotateDegrees(90);
                        canvas.Translate(0, src.Height);
                        canvas.Scale(1, -1);
                        break;
                    case SKEncodedOrigin.RightTop:      // 6: 90 度
                        canvas.Translate(w, 0);
                        canvas.RotateDegrees(90);
                        break;
                    case SKEncodedOrigin.RightBottom:   // 7: 左右反転して 270 度
                        canvas.Translate(w, 0);
                        canvas.RotateDegrees(90);
                        canvas.Translate(src.Width, 0);
                        canvas.Scale(-1, 1);
                        break;
                    case SKEncodedOrigin.LeftBottom:    // 8: 270 度
                        canvas.Translate(0, h);
                        canvas.RotateDegrees(270);
                        break;
                }
                canvas.DrawBitmap(src, 0, 0);
            }
            return dst;
        }

        /// <summary>SKBitmap（Bgra8888 / 乗算済み α）の画素を Avalonia の Bitmap に写す</summary>
        private static Bitmap ToBitmap(SKBitmap bmp)
        {
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, bmp.GetPixels(),
                new PixelSize(bmp.Width, bmp.Height), new Vector(96, 96), bmp.RowBytes);
        }

        /// <summary>画像の原寸の横幅（px）。ヘッダだけ読む。取れなければ 0</summary>
        public static int PixelWidthOf(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return 0;
            try
            {
                using (var codec = SKCodec.Create(path))
                {
                    if (codec == null) return 0;
                    // 90 度回る写真は、見た目の横幅は記録上の高さになる
                    SKEncodedOrigin o = codec.EncodedOrigin;
                    bool swap = o == SKEncodedOrigin.LeftTop || o == SKEncodedOrigin.RightTop ||
                                o == SKEncodedOrigin.RightBottom || o == SKEncodedOrigin.LeftBottom;
                    return swap ? codec.Info.Height : codec.Info.Width;
                }
            }
            catch { return 0; }
        }

        // ===== Windows のサムネイル =====

        /// <summary>
        /// Windows のサムネイルを取得する。動画のポスターフレームもこれで取れる。
        /// Windows 以外では null を返す（ffmpeg などを抱えないため）。
        ///
        /// allowIcon に true を渡すと、サムネイルを持たないファイルでは
        /// 種類アイコン（白紙アイコンなど）で代替する。既定は false。
        /// 中身の絵が欲しい用途で true にすると、壊れた画像がアイコンとして
        /// 表示されてしまい失敗に気づけないので注意。
        ///
        /// COM を使うので STA スレッドから呼ぶこと（Avalonia の UI スレッドは STA）。
        /// </summary>
        public static Bitmap ThumbnailFromFile(string path, int size = 0, bool allowIcon = false)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            if (size <= 0) size = ThumbnailSize;
            try { return WindowsThumbnail.Get(Path.GetFullPath(path), size, allowIcon); }
            catch { return null; }
        }
    }
}
