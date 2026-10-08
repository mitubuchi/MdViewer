/*
    File Name : MdViewer.cs
    Programmer: Keiji Mitsubuchi

    2026.10.08  created
                Markdown をそのまま表示するコントロール（MdWpf.MdViewer の Avalonia 版）。

    ■ Usage (XAML):
        <md:MdViewer Markdown="{Binding Text}" />
    ■ Usage (code):
        var v = new MdViewer();
        v.LoadFile(path);   // BasePath も設定され、相対パスの画像を解決する
 */
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using MdLib;

namespace MdAvalonia
{
    public class MdViewer : UserControl
    {
        /// <summary>拡大縮小の範囲（％）。WPF の FlowDocumentScrollViewer の既定と揃えてある</summary>
        public const double MinZoom = 20.0;
        public const double MaxZoom = 200.0;

        private readonly ScrollViewer _scroll;
        private readonly LayoutTransformControl _zoom;

        public MdViewer()
        {
            _zoom = new LayoutTransformControl();
            // 横はスクロールさせず、幅に合わせて折り返す（FlowDocumentScrollViewer と同じ見え方）
            _scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _zoom,
            };
            Content = _scroll;
            Rebuild();
        }

        // ===== Markdown =====
        public static readonly StyledProperty<string> MarkdownProperty =
            AvaloniaProperty.Register<MdViewer, string>(nameof(Markdown), "");

        /// <summary>表示する Markdown 文字列</summary>
        public string Markdown
        {
            get { return GetValue(MarkdownProperty); }
            set { SetValue(MarkdownProperty, value); }
        }

        // ===== BasePath =====
        public static readonly StyledProperty<string> BasePathProperty =
            AvaloniaProperty.Register<MdViewer, string>(nameof(BasePath), "");

        /// <summary>相対パスの画像・リンクを解決する基準ディレクトリ</summary>
        public string BasePath
        {
            get { return GetValue(BasePathProperty); }
            set { SetValue(BasePathProperty, value); }
        }

        // ===== Zoom =====
        public static readonly StyledProperty<double> ZoomProperty =
            AvaloniaProperty.Register<MdViewer, double>(nameof(Zoom), 100.0);

        /// <summary>表示倍率（％）。100 で等倍。20〜200 に収める</summary>
        public double Zoom
        {
            get { return GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        // ===== MdStyle =====
        public static readonly StyledProperty<MdStyle> StyleSetProperty =
            AvaloniaProperty.Register<MdViewer, MdStyle>(nameof(StyleSet));

        /// <summary>見た目の設定。null なら既定値（MdWpf と同じ名前にしてある）</summary>
        public MdStyle StyleSet
        {
            get { return GetValue(StyleSetProperty); }
            set { SetValue(StyleSetProperty, value); }
        }

        /// <summary>直近の解析結果（デバッグ・再利用用）</summary>
        public MdNode Tree { get; private set; }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == MarkdownProperty || change.Property == BasePathProperty ||
                change.Property == StyleSetProperty)
            {
                Rebuild();
            }
            else if (change.Property == ZoomProperty)
            {
                ApplyZoom();
            }
        }

        /// <summary>Markdown ファイルを読み込む。BasePath も自動で設定する</summary>
        public bool LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            try
            {
                string text = File.ReadAllText(path, System.Text.Encoding.UTF8);
                BasePath = Path.GetDirectoryName(Path.GetFullPath(path));
                Markdown = text;
                return true;
            }
            catch { return false; }
        }

        private void Rebuild()
        {
            // コンストラクターの途中（部品を作る前）にも呼ばれうる
            if (_zoom == null) return;

            MdStyle st = StyleSet ?? new MdStyle();
            // BasePath プロパティ側を優先する（StyleSet を共有していても壊れないよう複製）
            if (!string.IsNullOrEmpty(BasePath) && st.BasePath != BasePath)
            {
                st = st.Clone();
                st.BasePath = BasePath;
            }

            Tree = MdParser.Parse(Markdown ?? "");
            _zoom.Child = MdFlow.ToControl(Tree, st);
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            if (_zoom == null) return;

            double z = Zoom;
            if (z < MinZoom) z = MinZoom;
            if (z > MaxZoom) z = MaxZoom;
            _zoom.LayoutTransform = System.Math.Abs(z - 100.0) < 0.01
                ? null
                : new ScaleTransform(z / 100.0, z / 100.0);
        }
    }
}
