/*
    File Name : MdFlow.cs
    Programmer: Keiji Mitsubuchi

    2026.10.08  created
                MdParser の解析結果を Avalonia のコントロールに組み立てる（MdWpf.MdFlow の Avalonia 版）。

                Avalonia には FlowDocument が無いので、ブロックごとに部品を作って縦に並べる。
                段落・見出し・表のセルは MdTextBlock（選べる文字列）、リストは記号と中身の 2 列、
                引用は左罫線付きの枠。文字の選択はブロックの中でだけ効く（ブロックをまたいでは選べない）。

    ■ Usage:
        Control view = MdFlow.ToControl(markdownText);
        scrollViewer.Content = view;
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ImgAvalonia;
using MdLib;
using SvgAvalonia;

namespace MdAvalonia
{
    public static class MdFlow
    {
        /// <summary>Markdown 文字列をコントロールに変換する</summary>
        public static Control ToControl(string markdown, MdStyle style = null)
        {
            return ToControl(MdParser.Parse(markdown), style);
        }

        /// <summary>解析済みの木をコントロールに変換する</summary>
        public static Control ToControl(MdNode doc, MdStyle style = null)
        {
            if (style == null) style = new MdStyle();

            var page = new StackPanel { Margin = style.PagePadding };
            var images = new List<Image>();
            if (doc != null)
                AddBlocks(doc.Kids, page.Children, style, images);

            // FlowDocument は先頭ブロックの上の余白を取らない（ページの余白だけになる）
            if (page.Children.Count > 0)
            {
                Thickness m = page.Children[0].Margin;
                page.Children[0].Margin = new Thickness(m.Left, 0, m.Right, m.Bottom);
            }

            var root = new Border { Background = style.Background, Child = page };

            // 画像は枠より広くならないよう、表示幅に合わせて上限を絞る。
            // 狭いビューア欄に 600px の画像を置いても、はみ出さずに縮んで見える
            if (images.Count > 0)
            {
                root.SizeChanged += (s, e) =>
                {
                    double width = e.NewSize.Width - style.PagePadding.Left - style.PagePadding.Right;
                    for (int i = 0; i < images.Count; i++) FitImage(images[i], width, style);
                };
            }
            return root;
        }

        // ===== ブロック =====
        private static void AddBlocks(List<MdNode> nodes, Controls into, MdStyle st, List<Image> images)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                MdNode n = nodes[i];
                Control c;
                switch (n.Type)
                {
                    case MdType.Heading:    c = Heading(n, st, images);   break;
                    case MdType.Paragraph:  c = Para(n, st, images);      break;
                    case MdType.CodeBlock:  c = CodeBlock(n, st);         break;
                    case MdType.BulletList: c = Bullets(n, st, false, images); break;
                    case MdType.NumberList: c = Bullets(n, st, true, images);  break;
                    case MdType.Quote:      c = Quote(n, st, images);     break;
                    case MdType.Rule:       c = Rule(st);                 break;
                    case MdType.Table:      c = Table(n, st, images);     break;
                    default:
                        // 想定外はインラインとして段落に落とす
                        MdTextBlock t = NewText(st);
                        t.Margin = st.ParagraphMargin;
                        AddInlines(new List<MdNode> { n }, t.Inlines, t, st, images);
                        c = t;
                        break;
                }
                Append(into, c);
            }
        }

        /// <summary>
        /// 縦に積む。FlowDocument は隣り合う段落の余白を重ねる（大きいほうだけ空く）ので、
        /// StackPanel でも同じ間隔になるよう、上の余白から前の下の余白ぶんを差し引く。
        /// </summary>
        private static void Append(Controls into, Control c)
        {
            if (into.Count > 0)
            {
                double prevBottom = into[into.Count - 1].Margin.Bottom;
                Thickness m = c.Margin;
                c.Margin = new Thickness(m.Left, Math.Max(0, m.Top - prevBottom), m.Right, m.Bottom);
            }
            into.Add(c);
        }

        private static MdTextBlock NewText(MdStyle st)
        {
            return new MdTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = st.FontFamily,
                FontSize = st.FontSize,
                Foreground = st.Foreground,
                OpenLink = st.OpenLink,
            };
        }

        private static Control Heading(MdNode n, MdStyle st, List<Image> images)
        {
            MdTextBlock t = NewText(st);
            t.FontSize = st.HeadingSizeOf(n.Level);
            t.FontWeight = FontWeight.Bold;
            AddInlines(n.Kids, t.Inlines, t, st, images);

            // H1 / H2 は下線を引く
            if (n.Level <= 2)
            {
                return new Border
                {
                    BorderBrush = st.RuleBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(0, 0, 0, 4),
                    Margin = st.HeadingMargin,
                    Child = t,
                };
            }
            t.Margin = st.HeadingMargin;
            return t;
        }

        private static Control Para(MdNode n, MdStyle st, List<Image> images)
        {
            // 画像だけの段落は、文字の行に載せずにそのまま置く。
            // 行に載せると行の高さ（文字の下がり）のぶんだけ上下が空いてしまう
            if (n.Kids.Count == 1 && n.Kids[0].Type == MdType.Image)
            {
                Control block = ImageControl(n.Kids[0], st, images);
                if (block != null)
                {
                    block.Margin = st.ParagraphMargin;
                    block.HorizontalAlignment = HorizontalAlignment.Left;
                    return block;
                }
            }

            MdTextBlock t = NewText(st);
            t.Margin = st.ParagraphMargin;
            AddInlines(n.Kids, t.Inlines, t, st, images);
            return t;
        }

        private static Control CodeBlock(MdNode n, MdStyle st)
        {
            MdTextBlock t = NewText(st);
            t.FontFamily = st.CodeFontFamily;
            t.FontSize = st.CodeFontSize;
            t.Text = n.Text;
            return new Border
            {
                Background = st.CodeBackground,
                Padding = st.CodeBlockPadding,
                Margin = st.ParagraphMargin,
                Child = t,
            };
        }

        private static Control Bullets(MdNode n, MdStyle st, bool numbered, List<Image> images)
        {
            // 全項目がタスクなら、箇条書きの記号は出さずチェック字形だけにする
            bool allTask = n.Kids.Count > 0;
            for (int i = 0; i < n.Kids.Count; i++)
                if (n.Kids[i].Type == MdType.ListItem && !n.Kids[i].Task) { allTask = false; break; }

            var list = new StackPanel
            {
                Margin = new Thickness(0, 2, 0, 6),
            };

            int number = 0;
            for (int i = 0; i < n.Kids.Count; i++)
            {
                MdNode kid = n.Kids[i];
                if (kid.Type != MdType.ListItem) continue;
                number++;

                // 項目直下のインラインを 1 段落にまとめ、子リストは別ブロックにする
                var inl = new List<MdNode>();
                var blk = new List<MdNode>();
                for (int k = 0; k < kid.Kids.Count; k++)
                {
                    MdType t = kid.Kids[k].Type;
                    if (t == MdType.BulletList || t == MdType.NumberList ||
                        t == MdType.CodeBlock || t == MdType.Quote || t == MdType.Table)
                        blk.Add(kid.Kids[k]);
                    else
                        inl.Add(kid.Kids[k]);
                }

                var body = new StackPanel();
                MdTextBlock p = NewText(st);
                if (kid.Task)
                {
                    p.Inlines.Add(new Run(kid.Checked ? "☑ " : "☐ ")
                    {
                        FontFamily = new FontFamily("Segoe UI Symbol, MS Gothic, Apple Symbols, DejaVu Sans"),
                    });
                    p.BuildPosition += 2;
                }
                AddInlines(inl, p.Inlines, p, st, images);
                body.Children.Add(p);
                if (blk.Count > 0) AddBlocks(blk, body.Children, st, images);

                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                };
                if (allTask)
                {
                    body.Margin = new Thickness(4, 0, 0, 0);
                }
                else
                {
                    // WPF の List は左に 20 の余白を取り、そこへ記号を右寄せで置く
                    var marker = new TextBlock
                    {
                        Text = numbered ? number + "." : "•",
                        FontFamily = st.FontFamily,
                        FontSize = st.FontSize,
                        Foreground = st.Foreground,
                        MinWidth = 20,
                        TextAlignment = TextAlignment.Right,
                        Padding = new Thickness(0, 0, 6, 0),
                    };
                    row.Children.Add(marker);
                }
                Grid.SetColumn(body, 1);
                row.Children.Add(body);
                list.Children.Add(row);
            }
            return list;
        }

        private static Control Quote(MdNode n, MdStyle st, List<Image> images)
        {
            var inner = new StackPanel();
            AddBlocks(n.Kids, inner.Children, st, images);
            return new Border
            {
                BorderBrush = st.QuoteBar,
                BorderThickness = new Thickness(4, 0, 0, 0),
                Padding = st.QuotePadding,
                Margin = new Thickness(0, 4, 0, 8),
                Child = inner,
            };
        }

        private static Control Rule(MdStyle st)
        {
            return new Border
            {
                Height = 1,
                Background = st.RuleBrush,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 10, 0, 10),
            };
        }

        private static Control Table(MdNode n, MdStyle st, List<Image> images)
        {
            int cols = 0;
            for (int r = 0; r < n.Kids.Count; r++)
                if (n.Kids[r].Kids.Count > cols) cols = n.Kids[r].Kids.Count;

            // FlowDocument の表と同じく、列は幅いっぱいに等分する
            var grid = new Grid();
            for (int c = 0; c < cols; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

            int rowIndex = 0;
            for (int r = 0; r < n.Kids.Count; r++)
            {
                MdNode rowNode = n.Kids[r];
                if (rowNode.Type != MdType.TableRow) continue;
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                for (int c = 0; c < cols; c++)
                {
                    MdNode cellNode = c < rowNode.Kids.Count ? rowNode.Kids[c] : null;
                    MdTextBlock t = NewText(st);
                    if (cellNode != null)
                    {
                        AddInlines(cellNode.Kids, t.Inlines, t, st, images);
                        t.TextAlignment =
                            cellNode.Align == MdAlign.Center ? TextAlignment.Center :
                            cellNode.Align == MdAlign.Right ? TextAlignment.Right :
                            TextAlignment.Left;
                    }
                    if (rowNode.Header) t.FontWeight = FontWeight.Bold;

                    // 罫線は右と下だけ引き、表の外枠で左と上を足す（線が二重にならない）。
                    // WPF は 0.5 を 2 つ重ねて 1 にしているが、Avalonia では 0.5 が描かれない
                    var cell = new Border
                    {
                        BorderBrush = st.TableLine,
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Padding = new Thickness(6, 3, 6, 3),
                        Background = rowNode.Header ? st.TableHeaderBackground : null,
                        Child = t,
                    };
                    Grid.SetRow(cell, rowIndex);
                    Grid.SetColumn(cell, c);
                    grid.Children.Add(cell);
                }
                rowIndex++;
            }
            return new Border
            {
                BorderBrush = st.TableLine,
                BorderThickness = new Thickness(1, 1, 0, 0),
                Margin = st.ParagraphMargin,
                Child = grid,
            };
        }

        // ===== インライン =====
        private static void AddInlines(List<MdNode> nodes, InlineCollection into, MdTextBlock owner,
            MdStyle st, List<Image> images)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                MdNode n = nodes[i];
                switch (n.Type)
                {
                    case MdType.Text:
                        AddRun(into, owner, new Run(n.Text));
                        break;

                    case MdType.Break:
                        // LineBreak は位置の数え方が環境で変わる（Windows では 2 文字）ので、
                        // 1 文字で確実に数えられる改行文字で表す
                        AddRun(into, owner, new Run("\n"));
                        break;

                    case MdType.Code:
                        AddRun(into, owner, new Run(n.Text)
                        {
                            FontFamily = st.CodeFontFamily,
                            FontSize = st.CodeFontSize,
                            Foreground = st.CodeForeground,
                            Background = st.CodeBackground,
                        });
                        break;

                    case MdType.Bold:
                        {
                            var b = new Bold();
                            AddInlines(n.Kids, b.Inlines, owner, st, images);
                            into.Add(b);
                        }
                        break;

                    case MdType.Italic:
                        {
                            var it = new Italic();
                            AddInlines(n.Kids, it.Inlines, owner, st, images);
                            into.Add(it);
                        }
                        break;

                    case MdType.Strike:
                        {
                            var sp = new Span();
                            AddInlines(n.Kids, sp.Inlines, owner, st, images);
                            SetStrike(sp);
                            into.Add(sp);
                        }
                        break;

                    case MdType.Link:
                        Link(n, into, owner, st, images);
                        break;

                    case MdType.Image:
                        into.Add(Image(n, st, images));
                        owner.BuildPosition += 1;   // 埋め込み部品は 1 文字ぶん
                        break;

                    default:
                        // ブロックがインラインの位置に来た場合はテキストに落とす
                        AddRun(into, owner, new Run(n.PlainText()));
                        break;
                }
            }
        }

        private static void AddRun(InlineCollection into, MdTextBlock owner, Run run)
        {
            into.Add(run);
            owner.BuildPosition += run.Text == null ? 0 : run.Text.Length;
        }

        // 子の Run まで辿って個別に設定する（WPF では Span の設定が子へ伝わらなかったため、
        // 同じ作りにしておく。Avalonia で伝わる場合も害はない）
        private static void SetStrike(Inline inline)
        {
            inline.TextDecorations = TextDecorations.Strikethrough;
            var span = inline as Span;   // Bold / Italic も Span 派生
            if (span != null)
            {
                foreach (Inline kid in span.Inlines) SetStrike(kid);
            }
        }

        private static void Link(MdNode n, InlineCollection into, MdTextBlock owner, MdStyle st,
            List<Image> images)
        {
            // WPF の Hyperlink と同じく、色を変えて下線を引く
            var span = new Span
            {
                Foreground = st.LinkForeground,
                TextDecorations = TextDecorations.Underline,
            };
            int start = owner.BuildPosition;
            AddInlines(n.Kids, span.Inlines, owner, st, images);
            if (span.Inlines.Count == 0) AddRun(span.Inlines, owner, new Run(n.Url));
            into.Add(span);

            Uri uri = ResolveUri(n.Url, st.BasePath);
            owner.AddLink(start, owner.BuildPosition - start, uri, n.Url);
        }

        private static Inline Image(MdNode n, MdStyle st, List<Image> images)
        {
            Control c = ImageControl(n, st, images);
            return c != null ? (Inline)new InlineUIContainer(c) : AltRun(n);
        }

        /// <summary>
        /// 画像の部品。読めなかったら null（呼び出し側が alt に落とす）。
        /// ローカルはその場で読む。http などは取りに行く間 alt を出しておき、届いたら差し替える
        /// </summary>
        private static Control ImageControl(MdNode n, MdStyle st, List<Image> images)
        {
            Uri uri = ResolveUri(n.Url, st.BasePath);
            if (uri == null) return null;

            bool svg = n.Url.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

            if (uri.IsFile)
            {
                IImage src = svg
                    ? SvgRender.FromUri(uri)
                    : ImgRender.From(uri.LocalPath, DecodeWidthOf(st));
                return src == null ? null : NewImage(n, src, st, images);
            }

            var holder = new ContentControl { Content = AltText(n, st) };
            int decodeWidth = DecodeWidthOf(st);
            Task.Run(() =>
            {
                IImage src = svg ? SvgRender.FromUri(uri) : (IImage)ImgRender.FromUri(uri, decodeWidth);
                if (src == null) return;
                Dispatcher.UIThread.Post(() => holder.Content = NewImage(n, src, st, images));
            });
            return holder;
        }

        private static Image NewImage(MdNode n, IImage src, MdStyle st, List<Image> images)
        {
            var image = new Image
            {
                Source = src,
                Stretch = Stretch.Uniform,
                // 広げると Uniform で枠いっぱいまで拡大されてしまうので、左に寄せて原寸を保つ
                HorizontalAlignment = HorizontalAlignment.Left,
                // 原寸より大きくはしない。上限は ImageMaxWidth と表示幅の小さいほう
                Tag = src.Size.Width,
            };
            RenderOptions.SetBitmapInterpolationMode(image, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
            ToolTip.SetTip(image, n.Text != "" ? n.Text : n.Url);
            FitImage(image, double.PositiveInfinity, st);
            images.Add(image);
            return image;
        }

        private static void FitImage(Image image, double available, MdStyle st)
        {
            double natural = image.Tag is double ? (double)image.Tag : double.PositiveInfinity;
            double max = natural;
            if (st.ImageMaxWidth > 0) max = Math.Min(max, st.ImageMaxWidth);
            if (available > 0) max = Math.Min(max, available);
            image.MaxWidth = max;
        }

        private static Run AltRun(MdNode n)
        {
            return new Run("[" + n.Text + "]");
        }

        private static Control AltText(MdNode n, MdStyle st)
        {
            return new TextBlock
            {
                Text = "[" + n.Text + "]",
                FontFamily = st.FontFamily,
                FontSize = st.FontSize,
                Foreground = st.Foreground,
            };
        }

        /// <summary>
        /// デコード時に縮小する目標幅（px）。0 なら原寸でデコードする。
        /// 表示幅より大きめに取って、拡大表示に耐えるようにしている。
        /// </summary>
        private static int DecodeWidthOf(MdStyle st)
        {
            if (st.ImageMaxWidth <= 0 || st.ImageDecodeScale <= 0) return 0;
            return (int)(st.ImageMaxWidth * st.ImageDecodeScale);
        }

        /// <summary>相対パスは BasePath 基準で絶対 URI にする。解決できなければ null</summary>
        private static Uri ResolveUri(string url, string basePath)
        {
            if (string.IsNullOrEmpty(url)) return null;

            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri)) return uri;

            if (!string.IsNullOrEmpty(basePath))
            {
                try
                {
                    string full = Path.GetFullPath(Path.Combine(basePath, url));
                    if (Uri.TryCreate(full, UriKind.Absolute, out uri)) return uri;
                }
                catch { }
            }
            return null;
        }
    }
}
