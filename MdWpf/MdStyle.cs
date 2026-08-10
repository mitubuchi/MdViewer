using System.Windows;
using System.Windows.Media;
using MdLib;

namespace MdWpf
{
    /// <summary>
    /// FlowDocument 生成時の見た目。既定値をそのまま使うか、
    /// 必要な項目だけ差し替えて MdFlow.ToFlowDocument() に渡す。
    /// </summary>
    public class MdStyle
    {
        public FontFamily FontFamily = new FontFamily("Meiryo, Yu Gothic UI, Segoe UI");
        public FontFamily CodeFontFamily = new FontFamily("Consolas, MS Gothic");

        public double FontSize = 14.0;
        public double CodeFontSize = 13.0;

        /// <summary>見出し 1..6 の文字サイズ（index 0 が H1）</summary>
        public double[] HeadingSize = { 26, 22, 18, 16, 14, 13 };

        public Brush Foreground = Brushes.Black;
        public Brush Background = Brushes.Transparent;
        public Brush LinkForeground = new SolidColorBrush(Color.FromRgb(0x1A, 0x6F, 0xC4));
        public Brush CodeForeground = new SolidColorBrush(Color.FromRgb(0xC0, 0x34, 0x1D));
        public Brush CodeBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
        public Brush QuoteBar = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0));
        public Brush RuleBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
        public Brush TableLine = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0));
        public Brush TableHeaderBackground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));

        public Thickness PagePadding = new Thickness(12);
        public Thickness ParagraphMargin = new Thickness(0, 4, 0, 8);
        public Thickness HeadingMargin = new Thickness(0, 12, 0, 6);
        public Thickness CodeBlockPadding = new Thickness(8);
        public Thickness QuotePadding = new Thickness(10, 4, 4, 4);

        /// <summary>画像の最大表示幅。0 以下なら原寸</summary>
        public double ImageMaxWidth = 600.0;

        /// <summary>
        /// ローカル画像をデコードする際の倍率。ImageMaxWidth × この値でデコードする。
        /// 1 より大きくしておくと拡大表示しても粗くなりにくい。0 以下なら原寸でデコードする。
        /// 巨大な写真を全画素展開しないための指定なので、原寸より大きくは決してならない。
        /// </summary>
        public double ImageDecodeScale = 2.0;

        /// <summary>相対パスの画像・リンクを解決する基準ディレクトリ</summary>
        public string BasePath = "";

        public double HeadingSizeOf(int level)
        {
            if (level < 1) level = 1;
            if (level > HeadingSize.Length) level = HeadingSize.Length;
            return HeadingSize[level - 1];
        }
    }
}
