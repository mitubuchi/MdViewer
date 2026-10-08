/*
    File Name : SvgViewer.cs
    Programmer: Keiji Mitsubuchi

    2026.10.08  created
                SVG をそのまま表示するコントロール（SvgWpf.SvgViewer の Avalonia 版）。
                拡大縮小の意味は SvgWpf と同じ。

    ■ Usage (XAML):
        <svg:SvgViewer Source="C:\data\icon.svg" />
        <svg:SvgViewer SvgText="{Binding Xml}" Zoom="200" />
 */
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace SvgAvalonia
{
    public class SvgViewer : UserControl
    {
        private readonly ScrollViewer _scroll;
        private readonly LayoutTransformControl _zoom;
        private readonly Image _image;

        public SvgViewer()
        {
            _image = new Image { Stretch = Stretch.Uniform };
            _zoom = new LayoutTransformControl { Child = _image };
            // 拡大時にはみ出した分をスクロールできるようにする
            _scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _zoom,
            };
            Content = _scroll;
            ApplyZoom();
        }

        // ===== Source（ファイルパス または SVG の XML）=====
        public static readonly StyledProperty<string> SourceProperty =
            AvaloniaProperty.Register<SvgViewer, string>(nameof(Source), "");

        /// <summary>SVG ファイルのパス。先頭が '&lt;' なら XML 文字列として扱う</summary>
        public string Source
        {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        // ===== SvgText（XML を明示的に渡す）=====
        public static readonly StyledProperty<string> SvgTextProperty =
            AvaloniaProperty.Register<SvgViewer, string>(nameof(SvgText), "");

        /// <summary>SVG の XML 文字列</summary>
        public string SvgText
        {
            get { return GetValue(SvgTextProperty); }
            set { SetValue(SvgTextProperty, value); }
        }

        // ===== Stretch =====
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<SvgViewer, Stretch>(nameof(Stretch), Stretch.Uniform);

        public Stretch Stretch
        {
            get { return GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        // ===== Zoom =====
        public static readonly StyledProperty<double> ZoomProperty =
            AvaloniaProperty.Register<SvgViewer, double>(nameof(Zoom), 100.0);

        /// <summary>
        /// 表示倍率（％）。
        /// 100 のときは Stretch に従って枠に合わせる（既定動作）。
        /// 100 以外のときは原寸 × 倍率で描き、はみ出た分はスクロールする。
        /// </summary>
        public double Zoom
        {
            get { return GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        /// <summary>読み込みに成功したか</summary>
        public bool IsSvgLoaded { get; private set; }

        /// <summary>描画内容のサイズ。読めていなければ 0x0</summary>
        public Size SvgSize { get; private set; }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SourceProperty) Apply(SvgRender.From(Source), Source);
            else if (change.Property == SvgTextProperty) Apply(SvgRender.FromText(SvgText), null);
            // 拡大縮小中は Stretch.None を使うため、ApplyZoom 経由で反映する
            else if (change.Property == ZoomProperty || change.Property == StretchProperty) ApplyZoom();
        }

        /// <summary>SVG ファイルを読み込む</summary>
        public bool LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            Source = path;
            return IsSvgLoaded;
        }

        private void Apply(IImage src, string tip)
        {
            _image.Source = src;
            IsSvgLoaded = src != null;
            SvgSize = SvgRender.SizeOf(src);
            ToolTip.SetTip(this, string.IsNullOrEmpty(tip) || tip.TrimStart().StartsWith("<") ? null : tip);
            ApplyZoom();   // 読み込み時点でも枠合わせ／スクロールの状態を確定させる
        }

        private void ApplyZoom()
        {
            double z = Zoom;
            if (z <= 0) z = 100.0;

            // Stretch.None は「原寸で見る」指定なので、100% でも枠には合わせない
            bool fit = Math.Abs(z - 100.0) < 0.01 && Stretch != Stretch.None;

            if (fit)
            {
                // ScrollViewer は中身を無限の大きさで測るため、スクロールを
                // 有効にしたままだと Stretch が効かず原寸のままになる（WPF と同じ）。
                // 枠に合わせるときはスクロールを切って枠の大きさを伝える
                _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                _zoom.LayoutTransform = null;
                _image.Stretch = Stretch;
            }
            else
            {
                _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                _image.Stretch = Stretch.None;
                _zoom.LayoutTransform = Math.Abs(z - 100.0) < 0.01
                    ? null
                    : new ScaleTransform(z / 100.0, z / 100.0);
            }
        }
    }
}
