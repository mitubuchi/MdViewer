/*
    File Name : MdFlow.cs
    Programmer: Keiji Mitsubuchi

    2026.08.07  created
                MdParser の解析結果を WPF の FlowDocument に変換する。

    ■ Usage:
        FlowDocument doc = MdFlow.ToFlowDocument(markdownText);
        flowDocumentScrollViewer.Document = doc;
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using ImgWpf;
using MdLib;
using SvgWpf;

namespace MdWpf
{
    public static class MdFlow
    {
        /// <summary>Markdown 文字列を FlowDocument に変換する</summary>
        public static FlowDocument ToFlowDocument(string markdown, MdStyle style = null)
        {
            return ToFlowDocument(MdParser.Parse(markdown), style);
        }

        /// <summary>解析済みの木を FlowDocument に変換する</summary>
        public static FlowDocument ToFlowDocument(MdNode doc, MdStyle style = null)
        {
            if (style == null) style = new MdStyle();

            var fd = new FlowDocument
            {
                FontFamily = style.FontFamily,
                FontSize = style.FontSize,
                Foreground = style.Foreground,
                Background = style.Background,
                PagePadding = style.PagePadding,
                // 段組みにさせない（既定は幅に応じて2段以上になる）
                ColumnWidth = double.PositiveInfinity,
            };

            if (doc != null)
                AddBlocks(doc.Kids, fd.Blocks, style);

            return fd;
        }

        // ===== ブロック =====
        private static void AddBlocks(List<MdNode> nodes, BlockCollection into, MdStyle st)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                MdNode n = nodes[i];
                switch (n.Type)
                {
                    case MdType.Heading:    into.Add(Heading(n, st));    break;
                    case MdType.Paragraph:  into.Add(Para(n, st));       break;
                    case MdType.CodeBlock:  into.Add(CodeBlock(n, st));  break;
                    case MdType.BulletList: into.Add(Bullets(n, st, false)); break;
                    case MdType.NumberList: into.Add(Bullets(n, st, true));  break;
                    case MdType.Quote:      into.Add(Quote(n, st));      break;
                    case MdType.Rule:       into.Add(Rule(st));          break;
                    case MdType.Table:      into.Add(Table(n, st));      break;
                    default:
                        // 想定外はインラインとして段落に落とす
                        var p = new Paragraph { Margin = st.ParagraphMargin };
                        AddInlines(new List<MdNode> { n }, p.Inlines, st);
                        into.Add(p);
                        break;
                }
            }
        }

        private static Block Heading(MdNode n, MdStyle st)
        {
            var p = new Paragraph
            {
                FontSize = st.HeadingSizeOf(n.Level),
                FontWeight = FontWeights.Bold,
                Margin = st.HeadingMargin,
            };
            AddInlines(n.Kids, p.Inlines, st);

            // H1 / H2 は下線を引く
            if (n.Level <= 2)
            {
                p.BorderBrush = st.RuleBrush;
                p.BorderThickness = new Thickness(0, 0, 0, 1);
                p.Padding = new Thickness(0, 0, 0, 4);
            }
            return p;
        }

        private static Block Para(MdNode n, MdStyle st)
        {
            var p = new Paragraph { Margin = st.ParagraphMargin };
            AddInlines(n.Kids, p.Inlines, st);
            return p;
        }

        private static Block CodeBlock(MdNode n, MdStyle st)
        {
            var p = new Paragraph
            {
                FontFamily = st.CodeFontFamily,
                FontSize = st.CodeFontSize,
                Foreground = st.Foreground,
                Background = st.CodeBackground,
                Padding = st.CodeBlockPadding,
                Margin = st.ParagraphMargin,
            };
            // コードは折り返さず、そのまま流す
            string[] rows = n.Text.Split('\n');
            for (int i = 0; i < rows.Length; i++)
            {
                if (i > 0) p.Inlines.Add(new LineBreak());
                p.Inlines.Add(new Run(rows[i]));
            }
            return p;
        }

        private static Block Bullets(MdNode n, MdStyle st, bool numbered)
        {
            // 全項目がタスクなら、箇条書きの記号は出さずチェック字形だけにする
            bool allTask = n.Kids.Count > 0;
            for (int i = 0; i < n.Kids.Count; i++)
                if (n.Kids[i].Type == MdType.ListItem && !n.Kids[i].Task) { allTask = false; break; }

            var list = new List
            {
                MarkerStyle = allTask ? TextMarkerStyle.None
                                      : (numbered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc),
                Margin = new Thickness(0, 2, 0, 6),
                Padding = new Thickness(allTask ? 4 : 20, 0, 0, 0),
            };

            for (int i = 0; i < n.Kids.Count; i++)
            {
                MdNode kid = n.Kids[i];
                if (kid.Type != MdType.ListItem) continue;

                var li = new ListItem();
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

                var p = new Paragraph { Margin = new Thickness(0) };
                if (kid.Task)
                {
                    p.Inlines.Add(new Run(kid.Checked ? "☑ " : "☐ ")
                    {
                        FontFamily = new FontFamily("Segoe UI Symbol, MS Gothic"),
                    });
                }
                AddInlines(inl, p.Inlines, st);
                li.Blocks.Add(p);
                if (blk.Count > 0) AddBlocks(blk, li.Blocks, st);

                list.ListItems.Add(li);
            }
            return list;
        }

        private static Block Quote(MdNode n, MdStyle st)
        {
            var sec = new Section
            {
                BorderBrush = st.QuoteBar,
                BorderThickness = new Thickness(4, 0, 0, 0),
                Padding = st.QuotePadding,
                Margin = new Thickness(0, 4, 0, 8),
            };
            AddBlocks(n.Kids, sec.Blocks, st);
            if (sec.Blocks.Count == 0) sec.Blocks.Add(new Paragraph());
            return sec;
        }

        private static Block Rule(MdStyle st)
        {
            // 空 Paragraph に罫線を引く方式は行高で潰れるため、
            // 高さ 1 の Border を BlockUIContainer で載せる
            var bar = new Border
            {
                Height = 1,
                Background = st.RuleBrush,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            return new BlockUIContainer(bar)
            {
                Margin = new Thickness(0, 10, 0, 10),
            };
        }

        private static Block Table(MdNode n, MdStyle st)
        {
            var table = new System.Windows.Documents.Table
            {
                CellSpacing = 0,
                Margin = st.ParagraphMargin,
            };

            int cols = 0;
            for (int r = 0; r < n.Kids.Count; r++)
                if (n.Kids[r].Kids.Count > cols) cols = n.Kids[r].Kids.Count;
            for (int c = 0; c < cols; c++)
                table.Columns.Add(new TableColumn());

            var group = new TableRowGroup();
            table.RowGroups.Add(group);

            for (int r = 0; r < n.Kids.Count; r++)
            {
                MdNode rowNode = n.Kids[r];
                if (rowNode.Type != MdType.TableRow) continue;

                var row = new TableRow();
                if (rowNode.Header) row.Background = st.TableHeaderBackground;

                for (int c = 0; c < cols; c++)
                {
                    MdNode cellNode = c < rowNode.Kids.Count ? rowNode.Kids[c] : null;
                    var p = new Paragraph { Margin = new Thickness(0) };
                    if (cellNode != null)
                    {
                        AddInlines(cellNode.Kids, p.Inlines, st);
                        p.TextAlignment =
                            cellNode.Align == MdAlign.Center ? TextAlignment.Center :
                            cellNode.Align == MdAlign.Right ? TextAlignment.Right :
                            TextAlignment.Left;
                    }
                    var cell = new TableCell(p)
                    {
                        BorderBrush = st.TableLine,
                        BorderThickness = new Thickness(0.5),
                        Padding = new Thickness(6, 3, 6, 3),
                    };
                    if (rowNode.Header) cell.FontWeight = FontWeights.Bold;
                    row.Cells.Add(cell);
                }
                group.Rows.Add(row);
            }
            return table;
        }

        // ===== インライン =====
        private static void AddInlines(List<MdNode> nodes, InlineCollection into, MdStyle st)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                MdNode n = nodes[i];
                switch (n.Type)
                {
                    case MdType.Text:
                        into.Add(new Run(n.Text));
                        break;

                    case MdType.Break:
                        into.Add(new LineBreak());
                        break;

                    case MdType.Code:
                        into.Add(new Run(n.Text)
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
                            AddInlines(n.Kids, b.Inlines, st);
                            into.Add(b);
                        }
                        break;

                    case MdType.Italic:
                        {
                            var it = new Italic();
                            AddInlines(n.Kids, it.Inlines, st);
                            into.Add(it);
                        }
                        break;

                    case MdType.Strike:
                        {
                            var sp = new Span();
                            AddInlines(n.Kids, sp.Inlines, st);
                            SetStrike(sp);
                            into.Add(sp);
                        }
                        break;

                    case MdType.Link:
                        into.Add(Link(n, st));
                        break;

                    case MdType.Image:
                        {
                            Inline img = Image(n, st);
                            if (img != null) into.Add(img);
                        }
                        break;

                    default:
                        // ブロックがインラインの位置に来た場合はテキストに落とす
                        into.Add(new Run(n.PlainText()));
                        break;
                }
            }
        }

        // Span に TextDecorations を設定しても子の Run には伝わらないため、
        // 生成後に Run まで辿って個別に設定する
        private static void SetStrike(Inline inline)
        {
            var run = inline as Run;
            if (run != null)
            {
                run.TextDecorations = TextDecorations.Strikethrough;
                return;
            }
            var span = inline as Span;   // Bold / Italic / Hyperlink も Span 派生
            if (span != null)
            {
                span.TextDecorations = TextDecorations.Strikethrough;
                foreach (Inline kid in span.Inlines) SetStrike(kid);
            }
        }

        private static Inline Link(MdNode n, MdStyle st)
        {
            var link = new Hyperlink { Foreground = st.LinkForeground };
            AddInlines(n.Kids, link.Inlines, st);
            if (link.Inlines.Count == 0) link.Inlines.Add(new Run(n.Url));

            Uri uri = ResolveUri(n.Url, st.BasePath);
            if (uri != null)
            {
                link.NavigateUri = uri;
                link.ToolTip = n.Url;
                link.RequestNavigate += OnRequestNavigate;
            }
            return link;
        }

        private static void OnRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try { System.Diagnostics.Process.Start(e.Uri.ToString()); }
            catch { /* 既定のブラウザが無い等は無視 */ }
            e.Handled = true;
        }

        private static Inline Image(MdNode n, MdStyle st)
        {
            Uri uri = ResolveUri(n.Url, st.BasePath);
            if (uri == null) return new Run("[" + n.Text + "]");

            try
            {
                // SVG は SvgWpf、ローカルの画像・動画は ImgWpf、
                // それ以外（http:// や pack://）は Uri のまま ImgWpf に渡す
                ImageSource src;
                if (n.Url.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    src = SvgRender.FromUri(uri);
                else if (uri.IsFile)
                    src = ImgRender.From(uri.LocalPath, DecodeWidthOf(st));
                else
                    src = ImgRender.FromUri(uri);

                if (src == null) return new Run("[" + n.Text + "]");

                var image = new System.Windows.Controls.Image
                {
                    Source = src,
                    Stretch = Stretch.Uniform,
                    ToolTip = n.Text != "" ? n.Text : n.Url,
                };
                if (st.ImageMaxWidth > 0)
                {
                    image.MaxWidth = st.ImageMaxWidth;
                    if (src.Width > 0 && src.Width < st.ImageMaxWidth) image.Width = src.Width;
                }
                else if (src.Width > 0) image.Width = src.Width;

                return new InlineUIContainer(image);
            }
            catch
            {
                // 読めない画像は alt テキストで代替する
                return new Run("[" + n.Text + "]");
            }
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
