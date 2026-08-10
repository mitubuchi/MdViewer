/*
    File Name : MainWindow.xaml.cs
    Programmer: Keiji Mitsubuchi

    2026.08.07  created
                Markdown / SVG の閲覧専用ビューア。
                機能はファイルのオープンとクローズ、および拡大縮小のみ。
                編集はできない（MdViewer / SvgViewer はどちらも読み取り専用）。

    2026.08.10  Add ImgViewer : 画像と動画サムネイルもタブで開けるようにした
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ImgWpf;
using MdLib;
using MdWpf;
using SvgWpf;

namespace MdViewerApp
{
    public partial class MainWindow : Window
    {
        // 拡大縮小の段階（％）
        static readonly double[] ZoomSteps =
            { 50, 67, 80, 90, 100, 110, 125, 150, 175, 200, 250, 300, 400 };

        const string ImageSpec = "*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.tif;*.tiff;*.ico;*.webp;*.heic";
        const string VideoSpec = "*.mp4;*.m4v;*.mov;*.avi;*.wmv;*.mkv;*.webm;*.mpg;*.mpeg";

        const string Filter =
            "表示できるファイル|*.md;*.markdown;*.svg;" + ImageSpec + ";" + VideoSpec +
            "|Markdown (*.md;*.markdown)|*.md;*.markdown" +
            "|SVG (*.svg)|*.svg" +
            "|画像 (" + ImageSpec + ")|" + ImageSpec +
            "|動画 (" + VideoSpec + ")|" + VideoSpec +
            "|すべてのファイル (*.*)|*.*";

        public MainWindow()
        {
            InitializeComponent();

            // コマンドライン引数で渡されたファイルを開く
            string[] args = Environment.GetCommandLineArgs();
            var paths = new List<string>();
            for (int i = 1; i < args.Length; i++) paths.Add(args[i]);
            if (paths.Count > 0) OpenFiles(paths);

            UpdateStatus();
        }

        // ===== ファイルを開く =====
        private void OpenExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = Filter, Multiselect = true };
            if (dlg.ShowDialog() != true) return;
            OpenFiles(dlg.FileNames);
        }

        private void OpenFiles(IEnumerable<string> paths)
        {
            foreach (string p in paths) OpenOne(p);
            UpdateStatus();
        }

        private void OpenOne(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

            // 同じファイルが既に開いていれば、そのタブに切り替えるだけ
            TabItem found = FindTab(path);
            if (found != null) { tabs.SelectedItem = found; return; }

            FrameworkElement view;
            try
            {
                if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    var sv = new SvgViewer { Background = Brushes.White };
                    sv.Source = path;
                    if (!sv.IsSvgLoaded)
                    {
                        MessageBox.Show(this, "SVG として解析できませんでした。\r\n" + path,
                            "読み込み失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    view = sv;
                }
                else if (ImgRender.IsSupported(path))
                {
                    var iv = new ImgViewer { Background = Brushes.White };
                    iv.Source = path;
                    if (!iv.IsImageLoaded)
                    {
                        // 動画はサムネイルハンドラが無いと絵が取れない
                        string why = ImgRender.IsVideoFile(path)
                            ? "サムネイルを取得できませんでした。\r\nこの形式に対応するコーデックが入っていない可能性があります。\r\n"
                            : "画像として読み込めませんでした。\r\n";
                        MessageBox.Show(this, why + path,
                            "読み込み失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    view = iv;
                }
                else
                {
                    var mv = new MdViewer();
                    if (!mv.LoadFile(path))
                    {
                        MessageBox.Show(this, "ファイルを読み込めませんでした。\r\n" + path,
                            "読み込み失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    view = mv;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "読み込み失敗",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var tab = new TabItem
            {
                Tag = path,
                ToolTip = path,
                Content = view,
            };
            tab.Header = MakeHeader(Path.GetFileName(path), tab);
            tabs.Items.Add(tab);
            tabs.SelectedItem = tab;
        }

        /// <summary>タブ見出し。ファイル名と閉じるボタンを並べる</summary>
        private object MakeHeader(string name, TabItem tab)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = name,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var close = new Button
            {
                Content = "✕",
                Width = 16,
                Height = 16,
                Padding = new Thickness(0),
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 9,
                Focusable = false,
                ToolTip = "閉じる",
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
            };
            close.Click += (s, e) => { CloseTab(tab); e.Handled = true; };
            panel.Children.Add(close);

            return panel;
        }

        private TabItem FindTab(string path)
        {
            foreach (TabItem t in tabs.Items)
                if (string.Equals((string)t.Tag, path, StringComparison.OrdinalIgnoreCase))
                    return t;
            return null;
        }

        // ===== ファイルを閉じる =====
        private void CloseExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            CloseTab(tabs.SelectedItem as TabItem);
        }

        private void CloseTab(TabItem tab)
        {
            if (tab == null) return;
            int index = tabs.Items.IndexOf(tab);
            tabs.Items.Remove(tab);

            if (tabs.Items.Count > 0)
                tabs.SelectedIndex = Math.Min(index, tabs.Items.Count - 1);

            UpdateStatus();
        }

        private void HasTab(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = tabs != null && tabs.Items.Count > 0;
        }

        private void ExitClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ===== 拡大縮小 =====
        private void ZoomInExecuted(object sender, ExecutedRoutedEventArgs e) { StepZoom(+1); }
        private void ZoomOutExecuted(object sender, ExecutedRoutedEventArgs e) { StepZoom(-1); }

        private void ZoomResetClick(object sender, RoutedEventArgs e)
        {
            SetZoom(100.0);
        }

        /// <summary>段階表の隣へ移動する</summary>
        private void StepZoom(int dir)
        {
            double cur = GetZoom();
            if (double.IsNaN(cur)) return;

            int i = NearestStep(cur);
            // 現在値が段階の間にある場合、方向側の段階へ寄せる
            if (dir > 0 && ZoomSteps[i] <= cur) i++;
            else if (dir < 0 && ZoomSteps[i] >= cur) i--;

            if (i < 0) i = 0;
            if (i > ZoomSteps.Length - 1) i = ZoomSteps.Length - 1;
            SetZoom(ZoomSteps[i]);
        }

        private static int NearestStep(double z)
        {
            int best = 0;
            double diff = double.MaxValue;
            for (int i = 0; i < ZoomSteps.Length; i++)
            {
                double d = Math.Abs(ZoomSteps[i] - z);
                if (d < diff) { diff = d; best = i; }
            }
            return best;
        }

        /// <summary>選択中のタブの倍率。タブが無ければ NaN</summary>
        private double GetZoom()
        {
            var tab = tabs.SelectedItem as TabItem;
            if (tab == null) return double.NaN;

            var mv = tab.Content as MdViewer;
            if (mv != null) return mv.Zoom;
            var sv = tab.Content as SvgViewer;
            if (sv != null) return sv.Zoom;
            var iv = tab.Content as ImgViewer;
            if (iv != null) return iv.Zoom;
            return double.NaN;
        }

        private void SetZoom(double z)
        {
            var tab = tabs.SelectedItem as TabItem;
            if (tab == null) return;

            var mv = tab.Content as MdViewer;
            if (mv != null) mv.Zoom = z;
            var sv = tab.Content as SvgViewer;
            if (sv != null) sv.Zoom = z;
            var iv = tab.Content as ImgViewer;
            if (iv != null) iv.Zoom = z;

            UpdateStatus();
        }

        /// <summary>Ctrl + マウスホイールで拡大縮小</summary>
        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;
            if (tabs.Items.Count == 0) return;

            StepZoom(e.Delta > 0 ? +1 : -1);
            e.Handled = true;   // スクロールさせない
        }

        // ===== ドラッグ＆ドロップでも開く =====
        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null) OpenFiles(files);
            e.Handled = true;
        }

        // ===== 表示更新 =====
        private void tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TabControl の中の子コントロールが上げたイベントは無視する
            if (e.OriginalSource != tabs) return;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (textEmpty == null) return;   // InitializeComponent 前

            int n = tabs.Items.Count;
            textEmpty.Visibility = n == 0 ? Visibility.Visible : Visibility.Collapsed;

            var tab = tabs.SelectedItem as TabItem;
            if (tab == null)
            {
                textStatus.Text = "ファイルを開いてください（Ctrl+O、またはドラッグ＆ドロップ）";
                textZoom.Text = "100%";
                return;
            }

            string path = (string)tab.Tag;
            string info = path;

            var mv = tab.Content as MdViewer;
            if (mv != null && mv.Tree != null)
                info += "    （ブロック数 " + mv.Tree.Kids.Count + "）";

            var sv = tab.Content as SvgViewer;
            if (sv != null)
                info += string.Format("    （{0:0.##} x {1:0.##}）",
                    sv.SvgBounds.Width, sv.SvgBounds.Height);

            var iv = tab.Content as ImgViewer;
            if (iv != null)
                info += string.Format("    （{0:0} x {1:0} px{2}）",
                    iv.PixelSize.Width, iv.PixelSize.Height,
                    iv.IsThumbnail ? " サムネイル" : "");

            textStatus.Text = string.Format("[{0}/{1}]  {2}", tabs.SelectedIndex + 1, n, info);

            double z = GetZoom();
            textZoom.Text = double.IsNaN(z) ? "100%" : z.ToString("0") + "%";
        }
    }
}
