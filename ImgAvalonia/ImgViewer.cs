/*
    File Name : ImgViewer.cs
    Programmer: Keiji Mitsubuchi

    2026.10.08  created
                画像ファイルを表示するコントロール（ImgWpf.ImgViewer の Avalonia 版）。
                動画はサムネイル（ポスターフレーム）を表示する。Windows 以外では動画は出ない。
                拡大縮小の意味は SvgViewer と揃えてある。

    ■ Usage (XAML):
        <img:ImgViewer Source="C:\data\photo.jpg" />
        <img:ImgViewer Source="C:\data\clip.mp4" Zoom="200" />
 */
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ImgAvalonia
{
    public class ImgViewer : UserControl
    {
        /// <summary>
        /// 動画サムネイルを要求する一辺（px）。
        /// 拡大しても粗くなりにくいよう、既定のサムネイルより大きめに取る。
        /// </summary>
        public const int ThumbnailRequestSize = 1024;

        private readonly ScrollViewer _scroll;
        private readonly LayoutTransformControl _zoom;
        private readonly Image _image;

        public ImgViewer()
        {
            _image = new Image { Stretch = Stretch.Uniform };
            // 拡大したときに補間で溶けないようにする（等倍以下では効果がない）
            RenderOptions.SetBitmapInterpolationMode(_image, BitmapInterpolationMode.HighQuality);

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

        // ===== Source（ファイルパス）=====
        public static readonly StyledProperty<string> SourceProperty =
            AvaloniaProperty.Register<ImgViewer, string>(nameof(Source), "");

        /// <summary>画像または動画のファイルパス</summary>
        public string Source
        {
            get { return GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        // ===== Stretch =====
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<ImgViewer, Stretch>(nameof(Stretch), Stretch.Uniform);

        public Stretch Stretch
        {
            get { return GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        // ===== Zoom =====
        public static readonly StyledProperty<double> ZoomProperty =
            AvaloniaProperty.Register<ImgViewer, double>(nameof(Zoom), 100.0);

        /// <summary>
        /// 表示倍率（％）。
        /// 100 のときは Stretch に従って枠に合わせる（既定動作）。
        /// 100 以外のときは原寸 × 倍率で描き、はみ出た分はスクロールする。
        /// SvgViewer と同じ意味づけ。
        /// </summary>
        public double Zoom
        {
            get { return GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        /// <summary>読み込みに成功したか</summary>
        public bool IsImageLoaded { get; private set; }

        /// <summary>動画などをサムネイルで代替表示しているか</summary>
        public bool IsThumbnail { get; private set; }

        /// <summary>表示している画像の画素サイズ。読めていなければ 0x0</summary>
        public PixelSize PixelSize { get; private set; }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == SourceProperty) Load(Source);
            // 拡大縮小中は Stretch.None を使うため、ApplyZoom 経由で反映する
            else if (change.Property == ZoomProperty || change.Property == StretchProperty) ApplyZoom();
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
            Bitmap src = null;
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

        private void Apply(Bitmap src, bool thumb, string tip)
        {
            var old = _image.Source as IDisposable;
            _image.Source = src;
            if (old != null && !ReferenceEquals(old, src)) old.Dispose();

            IsImageLoaded = src != null;
            IsThumbnail = thumb;
            PixelSize = src == null ? default(PixelSize) : src.PixelSize;
            ToolTip.SetTip(this, string.IsNullOrEmpty(tip) ? null : tip);
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
