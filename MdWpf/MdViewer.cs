/*
    File Name : MdViewer.cs
    Programmer: Keiji Mitsubuchi

    2026.08.07  created
                Markdown をそのまま表示するコントロール。

    ■ Usage (XAML):
        <r:MdViewer Markdown="{Binding Text}" />
    ■ Usage (code):
        var v = new MdViewer();
        v.Markdown = File.ReadAllText(path);
        v.BasePath = Path.GetDirectoryName(path);   // 相対パスの画像を解決する
 */
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MdLib;

namespace MdWpf
{
    public class MdViewer : UserControl
    {
        private readonly FlowDocumentScrollViewer _viewer;

        public MdViewer()
        {
            _viewer = new FlowDocumentScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                IsToolBarVisible = false,
            };
            Content = _viewer;
            Rebuild();
        }

        // ===== Markdown =====
        public static readonly DependencyProperty MarkdownProperty =
            DependencyProperty.Register(nameof(Markdown), typeof(string), typeof(MdViewer),
                new PropertyMetadata("", OnChanged));

        /// <summary>表示する Markdown 文字列</summary>
        public string Markdown
        {
            get { return (string)GetValue(MarkdownProperty); }
            set { SetValue(MarkdownProperty, value); }
        }

        // ===== BasePath =====
        public static readonly DependencyProperty BasePathProperty =
            DependencyProperty.Register(nameof(BasePath), typeof(string), typeof(MdViewer),
                new PropertyMetadata("", OnChanged));

        /// <summary>相対パスの画像・リンクを解決する基準ディレクトリ</summary>
        public string BasePath
        {
            get { return (string)GetValue(BasePathProperty); }
            set { SetValue(BasePathProperty, value); }
        }

        // ===== Zoom =====
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(MdViewer),
                new PropertyMetadata(100.0, OnZoomChanged));

        /// <summary>表示倍率（％）。100 で等倍。FlowDocumentScrollViewer の Zoom に渡す</summary>
        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = (MdViewer)d;
            double z = (double)e.NewValue;
            if (z < v._viewer.MinZoom) z = v._viewer.MinZoom;
            if (z > v._viewer.MaxZoom) z = v._viewer.MaxZoom;
            v._viewer.Zoom = z;
        }

        // ===== MdStyle =====
        public static readonly DependencyProperty StyleSetProperty =
            DependencyProperty.Register(nameof(StyleSet), typeof(MdStyle), typeof(MdViewer),
                new PropertyMetadata(null, OnChanged));

        /// <summary>見た目の設定。null なら既定値（UserControl.Style と名前が衝突するため StyleSet）</summary>
        public MdStyle StyleSet
        {
            get { return (MdStyle)GetValue(StyleSetProperty); }
            set { SetValue(StyleSetProperty, value); }
        }

        /// <summary>直近の解析結果（デバッグ・再利用用）</summary>
        public MdNode Tree { get; private set; }

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MdViewer)d).Rebuild();
        }

        /// <summary>Markdown ファイルを読み込む。BasePath も自動で設定する</summary>
        public bool LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            try
            {
                string text = File.ReadAllText(path, System.Text.Encoding.UTF8);
                BasePath = Path.GetDirectoryName(path);
                Markdown = text;
                return true;
            }
            catch { return false; }
        }

        private void Rebuild()
        {
            MdStyle st = StyleSet ?? new MdStyle();
            // BasePath プロパティ側を優先する（StyleSet を共有していても壊れないよう複製）
            if (!string.IsNullOrEmpty(BasePath) && st.BasePath != BasePath)
            {
                st = Clone(st);
                st.BasePath = BasePath;
            }

            Tree = MdParser.Parse(Markdown);
            _viewer.Document = MdFlow.ToFlowDocument(Tree, st);
        }

        private static MdStyle Clone(MdStyle s)
        {
            return new MdStyle
            {
                FontFamily = s.FontFamily,
                CodeFontFamily = s.CodeFontFamily,
                FontSize = s.FontSize,
                CodeFontSize = s.CodeFontSize,
                HeadingSize = s.HeadingSize,
                Foreground = s.Foreground,
                Background = s.Background,
                LinkForeground = s.LinkForeground,
                CodeForeground = s.CodeForeground,
                CodeBackground = s.CodeBackground,
                QuoteBar = s.QuoteBar,
                RuleBrush = s.RuleBrush,
                TableLine = s.TableLine,
                TableHeaderBackground = s.TableHeaderBackground,
                PagePadding = s.PagePadding,
                ParagraphMargin = s.ParagraphMargin,
                HeadingMargin = s.HeadingMargin,
                CodeBlockPadding = s.CodeBlockPadding,
                QuotePadding = s.QuotePadding,
                ImageMaxWidth = s.ImageMaxWidth,
                ImageDecodeScale = s.ImageDecodeScale,
                BasePath = s.BasePath,
            };
        }
    }
}
