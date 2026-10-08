using Avalonia;
using Avalonia.Media;

namespace MdAvalonia
{
    /// <summary>
    /// 描画時の見た目（MdWpf.MdStyle の Avalonia 版）。既定値をそのまま使うか、
    /// 必要な項目だけ差し替えて MdFlow.ToControl() に渡す。値は MdWpf と揃えてある。
    /// </summary>
    public class MdStyle
    {
        // Windows の字形を先に並べ、無ければ macOS / Linux の日本語字形へ落ちるようにする
        public FontFamily FontFamily =
            new FontFamily("Meiryo, Yu Gothic UI, Segoe UI, Hiragino Sans, Noto Sans CJK JP");
        public FontFamily CodeFontFamily =
            new FontFamily("Consolas, MS Gothic, Menlo, DejaVu Sans Mono, Noto Sans Mono CJK JP");

        public double FontSize = 14.0;
        public double CodeFontSize = 13.0;

        /// <summary>見出し 1..6 の文字サイズ（index 0 が H1）</summary>
        public double[] HeadingSize = { 26, 22, 18, 16, 14, 13 };

        public IBrush Foreground = Brushes.Black;
        public IBrush Background = Brushes.Transparent;
        public IBrush LinkForeground = new SolidColorBrush(Color.FromRgb(0x1A, 0x6F, 0xC4));
        public IBrush CodeForeground = new SolidColorBrush(Color.FromRgb(0xC0, 0x34, 0x1D));
        public IBrush CodeBackground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
        public IBrush QuoteBar = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0));
        public IBrush RuleBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
        public IBrush TableLine = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0));
        public IBrush TableHeaderBackground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));

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

        /// <summary>
        /// リンクが押されたときの処理。null なら OS に開かせる（ブラウザーや関連付けられたアプリ）。
        /// ホストが自分で開きたいとき（マップ内の移動など）に差し替える。
        /// </summary>
        public System.Action<System.Uri> OpenLink;

        public double HeadingSizeOf(int level)
        {
            if (level < 1) level = 1;
            if (level > HeadingSize.Length) level = HeadingSize.Length;
            return HeadingSize[level - 1];
        }

        /// <summary>複製を作る（MdViewer が BasePath だけ差し替えるときに使う）</summary>
        public MdStyle Clone()
        {
            return (MdStyle)MemberwiseClone();
        }
    }
}
