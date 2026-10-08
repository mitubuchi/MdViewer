/*
    File Name : SvgRender.cs
    Programmer: Keiji Mitsubuchi

    2026.10.08  created
                SVG を Avalonia の IImage に変換する（SvgWpf.SvgRender の Avalonia 版）。
                SVG は仕様が大きいため、解析と描画は Svg.Skia に任せている。
                SvgWpf と同じく、失敗時は例外を投げず null を返す。

    ■ Usage:
        image.Source = SvgRender.FromFile(@"C:\data\icon.svg");
        image.Source = SvgRender.FromText(svgXmlString);
 */
using System;
using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace SvgAvalonia
{
    public static class SvgRender
    {
        /// <summary>SVG ファイルから作る。失敗したら null</summary>
        public static IImage FromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try { return ToImage(SvgSource.Load(Path.GetFullPath(path), null, null)); }
            catch { return null; }
        }

        /// <summary>SVG の XML 文字列から作る。失敗したら null</summary>
        public static IImage FromText(string svgXml)
        {
            if (string.IsNullOrEmpty(svgXml)) return null;
            try { return ToImage(SvgSource.LoadFromSvg(svgXml)); }
            catch { return null; }
        }

        /// <summary>Stream から作る。失敗したら null</summary>
        public static IImage FromStream(Stream stream)
        {
            if (stream == null) return null;
            try { return ToImage(SvgSource.LoadFromStream(stream, null)); }
            catch { return null; }
        }

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        /// <summary>
        /// Uri（file:// / http:// / https://）から作る。失敗したら null。
        /// http は取りに行くので時間がかかる。UI スレッドからは呼ばないこと。
        /// </summary>
        public static IImage FromUri(Uri uri)
        {
            if (uri == null) return null;
            try
            {
                if (uri.IsFile) return FromFile(uri.LocalPath);
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

                byte[] data = Http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
                using (var ms = new MemoryStream(data))
                    return FromStream(ms);
            }
            catch { return null; }
        }

        /// <summary>ファイルでも XML 文字列でも受け取る。判別は先頭が '&lt;' かどうか</summary>
        public static IImage From(string fileOrXml)
        {
            if (string.IsNullOrEmpty(fileOrXml)) return null;
            string s = fileOrXml.TrimStart();
            if (s.StartsWith("<")) return FromText(fileOrXml);
            return FromFile(fileOrXml);
        }

        private static IImage ToImage(SvgSource source)
        {
            // 読めなかったときも SvgSource 自体は返ってくることがあるので、絵の有無で判定する
            if (source == null) return null;
            if (source.Picture == null)
            {
                source.Dispose();
                return null;
            }
            return new SvgImage { Source = source };
        }

        /// <summary>SVG の描画サイズ。取れなければ 0x0</summary>
        public static Size SizeOf(IImage image)
        {
            return image == null ? default(Size) : image.Size;
        }
    }
}
