using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MdLib;
using MdWpf;
using SvgWpf;

namespace TestWpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            textMdInput.Text = MdSamples.Markdown;
            textSvgInput.Text = SvgSamples.Svg;
        }

        // ===== Markdown =====
        private void textMdInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            mdViewer.Markdown = textMdInput.Text;
            MdNode tree = mdViewer.Tree;
            textMdInfo.Text = tree == null ? "" : "ブロック数: " + tree.Kids.Count;
        }

        private void buttonMdOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Markdown (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt|すべて (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                textMdInput.Text = File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
                mdViewer.BasePath = Path.GetDirectoryName(dlg.FileName);
                textMdInfo.Text = Path.GetFileName(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "読み込み失敗");
            }
        }

        private void buttonMdSample_Click(object sender, RoutedEventArgs e)
        {
            textMdInput.Text = MdSamples.Markdown;
        }

        private void buttonMdTree_Click(object sender, RoutedEventArgs e)
        {
            MdNode tree = mdViewer.Tree;
            textTree.Text = tree == null ? "" : tree.Dump();
        }

        // ===== SVG =====
        private void textSvgInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            svgViewer.SvgText = textSvgInput.Text;
            ShowSvgInfo();
        }

        private void buttonSvgOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "SVG (*.svg)|*.svg|すべて (*.*)|*.*" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                textSvgInput.Text = File.ReadAllText(dlg.FileName, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "読み込み失敗");
            }
        }

        private void buttonSvgSample_Click(object sender, RoutedEventArgs e)
        {
            textSvgInput.Text = SvgSamples.Svg;
        }

        private void comboStretch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (svgViewer == null) return;
            var item = comboStretch.SelectedItem as ComboBoxItem;
            if (item == null) return;

            Stretch s;
            if (Enum.TryParse((string)item.Content, out s)) svgViewer.Stretch = s;
        }

        private void ShowSvgInfo()
        {
            if (!svgViewer.IsSvgLoaded) { textSvgInfo.Text = "読み込み失敗（SVG として解析できません）"; return; }
            Rect b = svgViewer.SvgBounds;
            textSvgInfo.Text = string.Format("サイズ {0:0.##} x {1:0.##}", b.Width, b.Height);
        }
    }
}
