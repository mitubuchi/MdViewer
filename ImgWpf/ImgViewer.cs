/*
    File Name : ImgViewer.cs
    Programmer: Keiji Mitsubuchi

    2026.08.10  created
                画像ファイルを表示するコントロール。
                動画はサムネイル（ポスターフレーム）を表示する。
                拡大縮小の意味は SvgViewer と揃えてある。

    ■ Usage (XAML):
        <img:ImgViewer Source="C:\data\photo.jpg" />
        <img:ImgViewer Source="C:\data\clip.mp4" Zoom="200" />
 */
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImgWpf
{
    public class ImgViewer : UserControl
    {
        /// <summary>
        /// 動画サムネイルを要求する一辺（px）。
        /// 拡大しても粗くなりにくいよう、既定のサムネイルより大きめに取る。
        /// </summary>
        public const int ThumbnailRequestSize = 1024;

        private readonly ScrollViewer _scroll;
        private readonly Image _image;

        public ImgViewer()
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

            // 拡大したときに補間で溶けないようにする（等倍以下では効果がない）
            RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);
        }

        // ===== Source（ファイルパス）=====
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(nameof(Source), typeof(string), typeof(ImgViewer),
                new PropertyMetadata("", OnSourceChanged));

        /// <summary>画像または動画のファイルパス</summary>
        public string Source
        {
            get { return (string)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        // ===== Stretch =====
        public static readonly DependencyProperty StretchProperty =
            DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(ImgViewer),
                new PropertyMetadata(Stretch.Uniform, OnStretchChanged));

        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        // ===== Zoom =====
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(ImgViewer),
                new PropertyMetadata(100.0, OnZoomChanged));

        /// <summary>
        /// 表示倍率（％）。
        /// 100 のときは Stretch に従って枠に合わせる（既定動作）。
        /// 100 以外のときは原寸 × 倍率で描き、はみ出た分はスクロールする。
        /// SvgViewer と同じ意味づけ。
        /// </summary>
        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        /// <summary>読み込みに成功したか</summary>
        public bool IsImageLoaded { get; private set; }

        /// <summary>動画などをサムネイルで代替表示しているか</summary>
        public bool IsThumbnail { get; private set; }

        /// <summary>表示している画像の画素サイズ。読めていなければ Size.Empty</summary>
        public Size PixelSize { get; private set; } = Size.Empty;

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ImgViewer)d).Load((string)e.NewValue);
        }

        private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ImgViewer)d).ApplyZoom();
        }

        private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 拡大縮小中は Stretch.None を使うため、ApplyZoom 経由で反映する
            ((ImgViewer)d).ApplyZoom();
        }

        /// <summary>画像または動画ファイルを読み込む</summary>
        public bool LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            Source = path;
            return IsImageLoaded;
        }

        private void Load(string path)
        {
            BitmapSource src = null;
            bool thumb = false;

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                // ビューアでは拡大して見るので、画像は縮小せず原寸で読む
                if (!ImgRender.IsVideoFile(path)) src = ImgRender.FromFile(path);
                if (src == null)
                {
                    src = ImgRender.ThumbnailFromFile(path, ThumbnailRequestSize);
                    thumb = src != null;
                }
            }
            Apply(src, thumb, path);
        }

        private void Apply(BitmapSource src, bool thumb, string tip)
        {
            _image.Source = src;
            IsImageLoaded = src != null;
            IsThumbnail = thumb;
            PixelSize = src == null
                ? Size.Empty
                : new Size(src.PixelWidth, src.PixelHeight);
            ToolTip = tip;
            ApplyZoom();
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
                // 有効にしたままだと Stretch が効かず原寸のままになる。
                // 枠に合わせるときはスクロールを切って枠の大きさを伝える
                _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                _image.LayoutTransform = null;
                _image.Stretch = Stretch;
            }
            else
            {
                _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                _image.Stretch = Stretch.None;
                _image.LayoutTransform = Math.Abs(z - 100.0) < 0.01
                    ? null
                    : new ScaleTransform(z / 100.0, z / 100.0);
            }
        }
    }
}
