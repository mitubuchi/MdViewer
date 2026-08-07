/*
    File Name : SvgViewer.cs
    Programmer: Keiji Mitsubuchi

    2026.08.07  created
                SVG をそのまま表示するコントロール。

    2026.08.07  Add Zoom : 拡大縮小（はみ出した分はスクロール）

    ■ Usage (XAML):
        <svg:SvgViewer Source="C:\data\icon.svg" />
        <svg:SvgViewer SvgText="{Binding Xml}" Zoom="200" />
 */
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SvgWpf
{
    public class SvgViewer : UserControl
    {
        private readonly ScrollViewer _scroll;
        private readonly Image _image;

        public SvgViewer()
        {
            _image = new Image { Stretch = Stretch.Uniform };
            // 拡大時にはみ出した分をスクロールできるようにする
            _scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _image,
            };
            Content = _scroll;
        }

        // ===== Source（ファイルパス または SVG の XML）=====
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(string), typeof(SvgViewer),
                new PropertyMetadata("", OnSourceChanged));

        /// <summary>SVG ファイルのパス。先頭が '&lt;' なら XML 文字列として扱う</summary>
        public string Source
        {
            get { return (string)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        // ===== SvgText（XML を明示的に渡す）=====
        public static readonly DependencyProperty SvgTextProperty =
            DependencyProperty.Register(nameof(SvgText), typeof(string), typeof(SvgViewer),
                new PropertyMetadata("", OnTextChanged));

        /// <summary>SVG の XML 文字列</summary>
        public string SvgText
        {
            get { return (string)GetValue(SvgTextProperty); }
            set { SetValue(SvgTextProperty, value); }
        }

        // ===== Stretch =====
        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(SvgViewer),
                new PropertyMetadata(Stretch.Uniform, OnStretchChanged));

        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        // ===== Zoom =====
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(SvgViewer),
                new PropertyMetadata(100.0, OnZoomChanged));

        /// <summary>
        /// 表示倍率（％）。
        /// 100 のときは Stretch に従って枠に合わせる（既定動作）。
        /// 100 以外のときは原寸 × 倍率で描き、はみ出た分はスクロールする。
        /// </summary>
        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SvgViewer)d).ApplyZoom();
        }

        private void ApplyZoom()
        {
            double z = Zoom;
            if (z <= 0) z = 100.0;

            if (Math.Abs(z - 100.0) < 0.01)
            {
                _image.LayoutTransform = null;
                _image.Stretch = Stretch;
            }
            else
            {
                _image.Stretch = Stretch.None;
                _image.LayoutTransform = new ScaleTransform(z / 100.0, z / 100.0);
            }
        }

        /// <summary>読み込みに成功したか</summary>
        public bool IsSvgLoaded { get; private set; }

        /// <summary>描画内容のサイズ。読めていなければ Rect.Empty</summary>
        public Rect SvgBounds { get; private set; } = Rect.Empty;

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = (SvgViewer)d;
            v.Apply(SvgRender.From((string)e.NewValue), (string)e.NewValue);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var v = (SvgViewer)d;
            v.Apply(SvgRender.FromText((string)e.NewValue), null);
        }

        private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 拡大縮小中は Stretch.None を使うため、ApplyZoom 経由で反映する
            ((SvgViewer)d).ApplyZoom();
        }

        /// <summary>SVG ファイルを読み込む</summary>
        public bool LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            Source = path;
            return IsSvgLoaded;
        }

        private void Apply(DrawingImage src, string tip)
        {
            _image.Source = src;
            IsSvgLoaded = src != null;
            SvgBounds = src == null ? Rect.Empty : new Rect(0, 0, src.Width, src.Height);
            ToolTip = tip;
        }
    }
}
