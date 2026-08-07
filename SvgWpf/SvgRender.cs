/*
    File Name : SvgRender.cs
    Programmer: Keiji Mitsubuchi

    2026.08.07  created
                SVG を WPF の DrawingImage / DrawingGroup に変換する。
                SVG は仕様が大きいため、解析は SharpVectors に任せている。

    ■ Usage:
        image.Source = SvgRender.FromFile(@"C:\data\icon.svg");
        image.Source = SvgRender.FromText(svgXmlString);
 */
using System;
using System.IO;
using System.Text;
using System.Windows.Media;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace SvgWpf
{
    public static class SvgRender
    {
        /// <summary>変換に使う既定設定。呼び出し側で差し替えられる</summary>
        public static WpfDrawingSettings Settings = NewSettings();

        private static WpfDrawingSettings NewSettings()
        {
            return new WpfDrawingSettings
            {
                IncludeRuntime = false,
                TextAsGeometry = false,   // テキストは文字として残す（選択・拡大に強い）
                OptimizePath = true,
            };
        }

        /// <summary>SVG ファイルから DrawingGroup を作る。失敗したら null</summary>
        public static DrawingGroup GroupFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var reader = new FileSvgReader(Settings);
                return reader.Read(path);
            }
            catch { return null; }
        }

        /// <summary>SVG の XML 文字列から DrawingGroup を作る。失敗したら null</summary>
        public static DrawingGroup GroupFromText(string svgXml)
        {
            if (string.IsNullOrEmpty(svgXml)) return null;
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(svgXml)))
                {
                    var reader = new FileSvgReader(Settings);
                    return reader.Read(ms);
                }
            }
            catch { return null; }
        }

        /// <summary>Stream から DrawingGroup を作る。失敗したら null</summary>
        public static DrawingGroup GroupFromStream(Stream stream)
        {
            if (stream == null) return null;
            try
            {
                var reader = new FileSvgReader(Settings);
                return reader.Read(stream);
            }
            catch { return null; }
        }

        /// <summary>Uri（file:// / http:// / pack://）から DrawingGroup を作る。失敗したら null</summary>
        public static DrawingGroup GroupFromUri(Uri uri)
        {
            if (uri == null) return null;
            try
            {
                var reader = new FileSvgReader(Settings);
                return reader.Read(uri);
            }
            catch { return null; }
        }

        // ===== DrawingImage（Image.Source にそのまま入る形）=====
        public static DrawingImage FromFile(string path) => ToImage(GroupFromFile(path));
        public static DrawingImage FromText(string svgXml) => ToImage(GroupFromText(svgXml));
        public static DrawingImage FromStream(Stream stream) => ToImage(GroupFromStream(stream));
        public static DrawingImage FromUri(Uri uri) => ToImage(GroupFromUri(uri));

        /// <summary>ファイルでも XML 文字列でも受け取る。判別は先頭が '&lt;' かどうか</summary>
        public static DrawingImage From(string fileOrXml)
        {
            if (string.IsNullOrEmpty(fileOrXml)) return null;
            string s = fileOrXml.TrimStart();
            if (s.StartsWith("<")) return FromText(fileOrXml);
            return FromFile(fileOrXml);
        }

        private static DrawingImage ToImage(DrawingGroup group)
        {
            if (group == null) return null;
            var di = new DrawingImage(group);
            di.Freeze();   // UI スレッド外でも使えるようにする
            return di;
        }

        /// <summary>SVG の描画サイズ（Bounds）。取れなければ Empty</summary>
        public static System.Windows.Rect Bounds(DrawingGroup group)
        {
            if (group == null) return System.Windows.Rect.Empty;
            return group.Bounds;
        }
    }
}
