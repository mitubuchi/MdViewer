/*
    File Name : MainWindow.cs
    Programmer: Keiji Mitsubuchi

    2026.10.08  created
                確認台の画面。Markdown / SVG / 画像 の 3 つのタブを持つ。
                Markdown は左で書き換えると右にすぐ反映される。ファイルはウィンドウへドロップしても開ける。
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ImgAvalonia;
using MdAvalonia;
using MdLib;
using SvgAvalonia;

namespace TestAvalonia
{
    internal class MainWindow : Window
    {
        private const string SampleSvg =
            "<svg xmlns='http://www.w3.org/2000/svg' width='240' height='160' viewBox='0 0 240 160'>" +
            "<rect x='10' y='10' width='220' height='140' rx='16' fill='#4A90D9'/>" +
            "<circle cx='80' cy='80' r='40' fill='#F5A623'/>" +
            "<text x='130' y='88' font-size='22' fill='white'>SVG テスト</text></svg>";

        private readonly TabControl _tabs;
        private readonly TextBox _mdInput;
        private readonly MdViewer _md;
        private readonly TextBlock _mdInfo;
        private readonly SvgViewer _svg;
        private readonly TextBlock _svgInfo;
        private readonly ImgViewer _img;
        private readonly TextBlock _imgInfo;

        public MainWindow()
        {
            Title = "MdAvalonia / SvgAvalonia / ImgAvalonia Test";
            Width = 1180;
            Height = 760;
            FontFamily = new FontFamily("Meiryo, Yu Gothic UI, Segoe UI, Hiragino Sans, Noto Sans CJK JP");

            // ===== Markdown =====
            _md = new MdViewer();
            _mdInfo = Info();
            _mdInput = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas, MS Gothic, Menlo, DejaVu Sans Mono"),
                FontSize = 13,
            };
            _mdInput.TextChanged += (s, e) =>
            {
                _md.Markdown = _mdInput.Text ?? "";
                _mdInfo.Text = (_mdInput.Text ?? "").Length + " 文字";
            };
            var mdTree = Button("解析木を表示", () => ShowTree());
            var mdBar = Bar(
                Button("MD を開く", () => Pick(new[] { "*.md", "*.markdown", "*.txt" })),
                Button("サンプル", () => { _md.BasePath = ""; _mdInput.Text = MdSamples.Markdown; }),
                mdTree,
                ZoomSlider(v => _md.Zoom = v, MdViewer.MinZoom, MdViewer.MaxZoom),
                _mdInfo);
            var mdPage = Split(mdBar, _mdInput, _md);

            // ===== SVG =====
            _svg = new SvgViewer();
            _svgInfo = Info();
            var svgBar = Bar(
                Button("SVG を開く", () => Pick(new[] { "*.svg" })),
                Button("サンプル", () => { _svg.SvgText = SampleSvg; ShowSvgInfo("サンプル"); }),
                StretchCombo(v => _svg.Stretch = v),
                ZoomSlider(v => _svg.Zoom = v, 10, 800),
                _svgInfo);
            var svgPage = Page(svgBar, WithHint(_svg, "SVG を開くか、ここへドロップしてください"));

            // ===== 画像 =====
            _img = new ImgViewer();
            _imgInfo = Info();
            var imgBar = Bar(
                Button("画像を開く", () => Pick(new[] { "*.*" })),
                StretchCombo(v => _img.Stretch = v),
                ZoomSlider(v => _img.Zoom = v, 10, 800),
                _imgInfo);
            var imgPage = Page(imgBar, WithHint(_img, "画像・動画を開くか、ここへドロップしてください"));

            _tabs = new TabControl
            {
                Margin = new Thickness(6),
                ItemsSource = new List<TabItem>
                {
                    new TabItem { Header = "Markdown", Content = mdPage },
                    new TabItem { Header = "SVG", Content = svgPage },
                    new TabItem { Header = "画像", Content = imgPage },
                },
            };
            Content = _tabs;

            // ファイルをウィンドウへドロップしても開ける
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DropEvent, OnDrop);

            _mdInput.Text = MdSamples.Markdown;

            // 開く前のタブが空だと、動いていないように見える。SVG は最初からサンプルを出しておく
            _svg.SvgText = SampleSvg;
            ShowSvgInfo("サンプル");
            _imgInfo.Text = "まだ何も開いていません";
        }

        /// <summary>何も読めていない間だけ、ビューアの下に案内の文字を見せる</summary>
        private static Control WithHint(Control viewer, string hint)
        {
            var text = new TextBlock
            {
                Text = hint,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
            };
            var panel = new Panel();
            panel.Children.Add(text);
            panel.Children.Add(viewer);
            viewer.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name != "Source" && e.Property.Name != "SvgText") return;
                var svg = viewer as SvgViewer;
                var img = viewer as ImgViewer;
                text.IsVisible = !((svg != null && svg.IsSvgLoaded) || (img != null && img.IsImageLoaded));
            };
            return panel;
        }

        /// <summary>拡張子でタブを選んで開く</summary>
        public void Open(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            path = Path.GetFullPath(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext == ".svg")
            {
                _svg.Source = path;
                ShowSvgInfo(path);
                _tabs.SelectedIndex = 1;
            }
            else if (ImgRender.IsSupported(path))
            {
                _img.Source = path;
                _imgInfo.Text = _img.IsImageLoaded
                    ? path + "  " + _img.PixelSize.Width + "×" + _img.PixelSize.Height +
                      (_img.IsThumbnail ? "（サムネイル）" : "")
                    : path + "  読めませんでした";
                _tabs.SelectedIndex = 2;
            }
            else
            {
                // 相対パスの画像を解決できるよう、先に BasePath を決めてから本文を入れる
                _md.BasePath = Path.GetDirectoryName(path);
                _mdInput.Text = File.ReadAllText(path, System.Text.Encoding.UTF8);
                _tabs.SelectedIndex = 0;
                Title = Path.GetFileName(path) + " - MdAvalonia Test";
            }
        }

        private void ShowSvgInfo(string what)
        {
            _svgInfo.Text = _svg.IsSvgLoaded
                ? what + "  " + _svg.SvgSize.Width + "×" + _svg.SvgSize.Height
                : what + "  読めませんでした";
        }

        private async void Pick(string[] patterns)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("対象") { Patterns = patterns } },
            });
            var file = files.FirstOrDefault();
            if (file != null) Open(file.TryGetLocalPath());
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
#pragma warning disable CS0618 // 11.3 では旧 API だが、11.x の範囲ではこちらが通る
            var files = e.Data.GetFiles();
#pragma warning restore CS0618
            var first = files == null ? null : files.FirstOrDefault();
            if (first != null) Open(first.TryGetLocalPath());
        }

        private void ShowTree()
        {
            var text = new TextBox
            {
                Text = _md.Tree != null ? _md.Tree.Dump() : "",
                IsReadOnly = true,
                AcceptsReturn = true,
                FontFamily = new FontFamily("Consolas, MS Gothic, Menlo, DejaVu Sans Mono"),
                FontSize = 12,
            };
            new Window { Title = "解析木", Width = 600, Height = 700, Content = text }.Show(this);
        }

        // ===== 部品 =====

        private static TextBlock Info()
        {
            return new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
                Margin = new Thickness(6, 0, 0, 0),
            };
        }

        private static Button Button(string text, Action click)
        {
            var b = new Button { Content = text, Margin = new Thickness(0, 0, 6, 0) };
            b.Click += (s, e) => click();
            return b;
        }

        private static StackPanel Bar(params Control[] items)
        {
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            foreach (var c in items) bar.Children.Add(c);
            return bar;
        }

        private static Control ZoomSlider(Action<double> apply, double min, double max)
        {
            var label = new TextBlock { Text = "100%", Width = 48, VerticalAlignment = VerticalAlignment.Center };
            var slider = new Slider { Minimum = min, Maximum = max, Value = 100, Width = 160, VerticalAlignment = VerticalAlignment.Center };
            slider.PropertyChanged += (s, e) =>
            {
                if (e.Property != RangeBase.ValueProperty) return;
                double v = Math.Round(slider.Value / 10) * 10;
                label.Text = v + "%";
                apply(v);
            };
            var reset = Button("標準に戻す", () => slider.Value = 100);
            return Bar(new TextBlock { Text = "拡大:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 4, 0) },
                slider, label, reset);
        }

        private static Control StretchCombo(Action<Stretch> apply)
        {
            var combo = new ComboBox
            {
                ItemsSource = new[] { Stretch.Uniform, Stretch.None, Stretch.Fill, Stretch.UniformToFill },
                SelectedIndex = 0,
                Width = 140,
                VerticalAlignment = VerticalAlignment.Center,
            };
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedItem is Stretch) apply((Stretch)combo.SelectedItem);
            };
            return Bar(new TextBlock { Text = "Stretch:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 4, 0) },
                combo);
        }

        private static Control Page(Control bar, Control body)
        {
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
            grid.Children.Add(bar);
            var frame = Frame(body);
            Grid.SetRow(frame, 1);
            grid.Children.Add(frame);
            return grid;
        }

        private static Control Split(Control bar, Control left, Control right)
        {
            var grid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*"),
                ColumnDefinitions = new ColumnDefinitions("*,4,*"),
            };
            Grid.SetColumnSpan(bar, 3);
            grid.Children.Add(bar);

            Grid.SetRow(left, 1);
            grid.Children.Add(left);

            var splitter = new GridSplitter { Background = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)) };
            Grid.SetRow(splitter, 1);
            Grid.SetColumn(splitter, 1);
            grid.Children.Add(splitter);

            var frame = Frame(right);
            Grid.SetRow(frame, 1);
            Grid.SetColumn(frame, 2);
            grid.Children.Add(frame);
            return grid;
        }

        private static Border Frame(Control body)
        {
            return new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                Child = body,
            };
        }
    }
}
