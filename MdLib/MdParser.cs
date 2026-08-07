/*
    File Name : MdParser.cs
    Programmer: Keiji Mitsubuchi

    2026.08.07  created
                Markdown を MdNode の木に解析する。外部ライブラリ非依存。

    対応するブロック要素:
        見出し      # ～ ######
        コード      ``` で囲む（言語名の指定可）／4スペースまたはタブのインデント
        引用        > （入れ子可）
        箇条書き    - * +（インデントで入れ子）
        番号付き    1. 2. …（インデントで入れ子）
        水平線      --- *** ___（3個以上）
        表          | a | b | と |---|:--:|--:| の組み合わせ
        段落        上記以外の連続行

    対応するインライン要素:
        コード      `code`
        強調        **bold** __bold__ *italic* _italic_ ***both***
        リンク      [text](url)
        画像        ![alt](url)
        改行        行末の半角スペース2個
        エスケープ  \* \_ \` \[ \] \( \) \# \\
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MdLib
{
    public static class MdParser
    {
        static readonly Regex ReHeading = new Regex(@"^(#{1,6})\s+(.*)$");
        static readonly Regex ReFence   = new Regex(@"^\s*```\s*([^\s`]*)\s*$");
        static readonly Regex ReRule    = new Regex(@"^\s*((-\s*){3,}|(\*\s*){3,}|(_\s*){3,})$");
        static readonly Regex ReBullet  = new Regex(@"^(\s*)[-*+]\s+(.*)$");
        static readonly Regex ReNumber  = new Regex(@"^(\s*)\d+[.)]\s+(.*)$");
        static readonly Regex ReQuote   = new Regex(@"^\s*>\s?(.*)$");
        static readonly Regex ReTableSep =
            new Regex(@"^\s*\|?\s*:?-{1,}:?\s*(\|\s*:?-{1,}:?\s*)*\|?\s*$");

        /// <summary>Markdown 文字列を解析して Document ノードを返す</summary>
        public static MdNode Parse(string md)
        {
            var doc = new MdNode(MdType.Document);
            if (string.IsNullOrEmpty(md)) return doc;

            string[] lines = md.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int i = 0;
            ParseBlocks(lines, ref i, lines.Length, 0, doc);
            return doc;
        }

        // ===== ブロック解析 =====
        // baseIndent より浅いインデントの行に出会ったら呼び出し元に戻る（入れ子の終端）
        private static void ParseBlocks(string[] lines, ref int i, int end, int baseIndent, MdNode parent)
        {
            while (i < end)
            {
                string line = lines[i];

                if (line.Trim().Length == 0) { i++; continue; }

                int indent = Indent(line);
                if (indent < baseIndent) return;

                // --- 水平線（箇条書きの "- " より先に判定する）---
                if (ReRule.IsMatch(line)) { parent.Add(new MdNode(MdType.Rule)); i++; continue; }

                // --- 見出し ---
                Match m = ReHeading.Match(line.TrimStart());
                if (m.Success)
                {
                    var h = parent.Add(new MdNode(MdType.Heading));
                    h.Level = m.Groups[1].Value.Length;
                    ParseInline(m.Groups[2].Value.TrimEnd(), h);
                    i++;
                    continue;
                }

                // --- フェンス付きコードブロック ---
                m = ReFence.Match(line);
                if (m.Success)
                {
                    var code = parent.Add(new MdNode(MdType.CodeBlock));
                    code.Url = m.Groups[1].Value;   // 言語名
                    var sb = new StringBuilder();
                    i++;
                    while (i < end && !ReFence.IsMatch(lines[i]))
                    {
                        if (sb.Length > 0) sb.Append("\n");
                        sb.Append(Unindent(lines[i], indent));
                        i++;
                    }
                    if (i < end) i++;   // 閉じフェンス
                    code.Text = sb.ToString();
                    continue;
                }

                // --- インデントコードブロック（baseIndent + 4 以上）---
                if (indent >= baseIndent + 4)
                {
                    var code = parent.Add(new MdNode(MdType.CodeBlock));
                    var sb = new StringBuilder();
                    while (i < end &&
                           (lines[i].Trim().Length == 0 || Indent(lines[i]) >= baseIndent + 4))
                    {
                        // 末尾の空行は取り込まない
                        if (lines[i].Trim().Length == 0)
                        {
                            int j = i + 1;
                            while (j < end && lines[j].Trim().Length == 0) j++;
                            if (j >= end || Indent(lines[j]) < baseIndent + 4) break;
                            sb.Append("\n");
                            i = j;
                            continue;
                        }
                        if (sb.Length > 0) sb.Append("\n");
                        sb.Append(Unindent(lines[i], baseIndent + 4));
                        i++;
                    }
                    code.Text = sb.ToString();
                    continue;
                }

                // --- 引用 ---
                if (ReQuote.IsMatch(line))
                {
                    var quote = parent.Add(new MdNode(MdType.Quote));
                    var inner = new List<string>();
                    while (i < end && ReQuote.IsMatch(lines[i]))
                    {
                        inner.Add(ReQuote.Match(lines[i]).Groups[1].Value);
                        i++;
                    }
                    string[] arr = inner.ToArray();
                    int k = 0;
                    ParseBlocks(arr, ref k, arr.Length, 0, quote);   // 引用の中は再帰
                    continue;
                }

                // --- 表 ---
                if (line.Contains("|") && i + 1 < end && ReTableSep.IsMatch(lines[i + 1]))
                {
                    ParseTable(lines, ref i, end, parent);
                    continue;
                }

                // --- 箇条書き / 番号付き ---
                if (ReBullet.IsMatch(line) || ReNumber.IsMatch(line))
                {
                    ParseList(lines, ref i, end, indent, parent);
                    continue;
                }

                // --- 段落 ---
                var para = parent.Add(new MdNode(MdType.Paragraph));
                bool first = true;
                while (i < end)
                {
                    string t = lines[i];
                    if (t.Trim().Length == 0) break;
                    if (ReRule.IsMatch(t) || ReHeading.IsMatch(t.TrimStart()) ||
                        ReFence.IsMatch(t) || ReQuote.IsMatch(t) ||
                        ReBullet.IsMatch(t) || ReNumber.IsMatch(t)) break;

                    if (!first)
                    {
                        // 直前の行末が半角スペース2個なら明示的な改行
                        para.Add(new MdNode(MdType.Break));
                    }
                    ParseInline(t.Trim(), para);
                    first = false;
                    i++;
                }
                // 空段落は捨てる
                if (!para.HasKids) parent.Kids.Remove(para);
            }
        }

        static readonly Regex ReTask = new Regex(@"^\[([ xX])\]\s+(.*)$");

        // ===== リスト =====
        // 項目の続き（深いインデントの行）はまとめて集め、字下げを外して
        // ParseBlocks に再帰させる。こうすると入れ子リストだけでなく
        // 項目内のコードブロックや引用も項目の子として扱えて、
        // 番号付きリストが途中で振り直されることがなくなる。
        private static void ParseList(string[] lines, ref int i, int end, int indent, MdNode parent)
        {
            bool numbered = ReNumber.IsMatch(lines[i]) && !ReBullet.IsMatch(lines[i]);
            var list = parent.Add(new MdNode(numbered ? MdType.NumberList : MdType.BulletList));
            list.Level = indent;

            while (i < end)
            {
                // 空行はまたぎ、次が同じリストの続きかを見る
                if (lines[i].Trim().Length == 0)
                {
                    int j = i + 1;
                    while (j < end && lines[j].Trim().Length == 0) j++;
                    if (j >= end) break;
                    int nind = Indent(lines[j]);
                    bool marker = ReBullet.IsMatch(lines[j]) || ReNumber.IsMatch(lines[j]);
                    if (nind > indent || (nind == indent && marker)) { i = j; continue; }
                    break;
                }

                int ind = Indent(lines[i]);
                if (ind < indent) break;

                Match m = numbered ? ReNumber.Match(lines[i]) : ReBullet.Match(lines[i]);
                // 同じ深さで別種のマーカーが来たら、別リストとして呼び出し元に返す
                if (!m.Success || ind != indent) break;

                var item = list.Add(new MdNode(MdType.ListItem));
                item.Level = ind;

                string body = m.Groups[2].Value.TrimEnd();
                Match tm = ReTask.Match(body);
                if (tm.Success)
                {
                    item.Task = true;
                    item.Checked = tm.Groups[1].Value != " ";
                    body = tm.Groups[2].Value;
                }
                ParseInline(body, item);
                i++;

                // --- 項目の続きを集める ---
                var cont = new List<string>();
                while (i < end)
                {
                    if (lines[i].Trim().Length == 0)
                    {
                        int j = i + 1;
                        while (j < end && lines[j].Trim().Length == 0) j++;
                        if (j < end && Indent(lines[j]) > indent) { cont.Add(""); i = j; continue; }
                        break;
                    }
                    if (Indent(lines[i]) <= indent) break;
                    cont.Add(lines[i]);
                    i++;
                }

                if (cont.Count > 0)
                {
                    // 一番浅い続き行に合わせて字下げを外す
                    int min = int.MaxValue;
                    for (int k = 0; k < cont.Count; k++)
                        if (cont[k].Trim().Length > 0) min = Math.Min(min, Indent(cont[k]));
                    if (min == int.MaxValue) min = 0;

                    var arr = new string[cont.Count];
                    for (int k = 0; k < cont.Count; k++) arr[k] = Unindent(cont[k], min);

                    int p = 0;
                    ParseBlocks(arr, ref p, arr.Length, 0, item);
                }
            }
        }

        // ===== 表 =====
        private static void ParseTable(string[] lines, ref int i, int end, MdNode parent)
        {
            var table = parent.Add(new MdNode(MdType.Table));

            string[] head = SplitRow(lines[i]);
            MdAlign[] aligns = ParseAligns(lines[i + 1], head.Length);
            i += 2;

            var hrow = table.Add(new MdNode(MdType.TableRow));
            hrow.Header = true;
            for (int c = 0; c < head.Length; c++)
            {
                var cell = hrow.Add(new MdNode(MdType.TableCell));
                cell.Align = aligns[c];
                ParseInline(head[c], cell);
            }

            while (i < end && lines[i].Contains("|") && lines[i].Trim().Length > 0)
            {
                string[] cols = SplitRow(lines[i]);
                var row = table.Add(new MdNode(MdType.TableRow));
                for (int c = 0; c < head.Length; c++)
                {
                    var cell = row.Add(new MdNode(MdType.TableCell));
                    cell.Align = aligns[c];
                    if (c < cols.Length) ParseInline(cols[c], cell);
                }
                i++;
            }
        }

        private static string[] SplitRow(string line)
        {
            string s = line.Trim();
            if (s.StartsWith("|")) s = s.Substring(1);
            if (s.EndsWith("|")) s = s.Substring(0, s.Length - 1);

            // \| はセル区切りにしない
            var cells = new List<string>();
            var sb = new StringBuilder();
            for (int k = 0; k < s.Length; k++)
            {
                if (s[k] == '\\' && k + 1 < s.Length && s[k + 1] == '|') { sb.Append('|'); k++; continue; }
                if (s[k] == '|') { cells.Add(sb.ToString().Trim()); sb.Length = 0; continue; }
                sb.Append(s[k]);
            }
            cells.Add(sb.ToString().Trim());
            return cells.ToArray();
        }

        private static MdAlign[] ParseAligns(string sep, int count)
        {
            string[] parts = SplitRow(sep);
            var r = new MdAlign[count];
            for (int c = 0; c < count; c++)
            {
                string p = c < parts.Length ? parts[c].Trim() : "";
                bool left = p.StartsWith(":");
                bool right = p.EndsWith(":");
                if (left && right) r[c] = MdAlign.Center;
                else if (right) r[c] = MdAlign.Right;
                else r[c] = MdAlign.Left;
            }
            return r;
        }

        // ===== インライン解析 =====
        /// <summary>1 行分のインライン要素を解析して parent に追加する</summary>
        public static void ParseInline(string s, MdNode parent)
        {
            if (string.IsNullOrEmpty(s)) return;

            bool hardBreak = s.EndsWith("  ");
            s = s.TrimEnd();

            var sb = new StringBuilder();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];

                // エスケープ
                if (c == '\\' && i + 1 < s.Length && IsEscapable(s[i + 1]))
                {
                    sb.Append(s[i + 1]);
                    i += 2;
                    continue;
                }

                // インラインコード。開きと同じ本数のバッククォートで閉じる
                // （`` `default(T)` `` のように中身にバッククォートを含められる）
                if (c == '`')
                {
                    int n = RunLength(s, i, '`');
                    int close = FindTickRun(s, i + n, n);
                    if (close > 0)
                    {
                        Flush(sb, parent);
                        string body = s.Substring(i + n, close - i - n);
                        // 前後に空白が1つずつある場合は取り除く（CommonMark の規則）
                        if (body.Length >= 2 && body[0] == ' ' && body[body.Length - 1] == ' ')
                            body = body.Substring(1, body.Length - 2);
                        parent.Add(new MdNode(MdType.Code, body));
                        i = close + n;
                        continue;
                    }
                }

                // <https://...> 形式の自動リンク
                if (c == '<')
                {
                    int gt = s.IndexOf('>', i + 1);
                    if (gt > i + 1)
                    {
                        string inner = s.Substring(i + 1, gt - i - 1);
                        if (IsUrl(inner))
                        {
                            Flush(sb, parent);
                            var al = parent.Add(new MdNode(MdType.Link));
                            al.Url = inner;
                            al.Add(new MdNode(MdType.Text, inner));
                            i = gt + 1;
                            continue;
                        }
                    }
                }

                // 裸の URL（http:// https://）
                if ((c == 'h' || c == 'H') && IsUrlStart(s, i))
                {
                    int e2 = UrlEnd(s, i);
                    if (e2 > i)
                    {
                        Flush(sb, parent);
                        string url = s.Substring(i, e2 - i);
                        var al = parent.Add(new MdNode(MdType.Link));
                        al.Url = url;
                        al.Add(new MdNode(MdType.Text, url));
                        i = e2;
                        continue;
                    }
                }

                // 打ち消し線 ~~text~~
                if (c == '~' && i + 1 < s.Length && s[i + 1] == '~')
                {
                    int close = s.IndexOf("~~", i + 2, StringComparison.Ordinal);
                    if (close > i + 2)
                    {
                        Flush(sb, parent);
                        var st = parent.Add(new MdNode(MdType.Strike));
                        ParseInline(s.Substring(i + 2, close - i - 2), st);
                        i = close + 2;
                        continue;
                    }
                }

                // 画像 ![alt](url)
                if (c == '!' && i + 1 < s.Length && s[i + 1] == '[')
                {
                    int tEnd, uEnd;
                    if (TryLink(s, i + 1, out tEnd, out uEnd))
                    {
                        Flush(sb, parent);
                        var img = parent.Add(new MdNode(MdType.Image));
                        img.Text = s.Substring(i + 2, tEnd - i - 2);
                        img.Url = s.Substring(tEnd + 2, uEnd - tEnd - 2);
                        i = uEnd + 1;
                        continue;
                    }
                }

                // リンク [text](url)
                if (c == '[')
                {
                    int tEnd, uEnd;
                    if (TryLink(s, i, out tEnd, out uEnd))
                    {
                        Flush(sb, parent);
                        var link = parent.Add(new MdNode(MdType.Link));
                        link.Url = s.Substring(tEnd + 2, uEnd - tEnd - 2);
                        // 表示文字は子ノードが持つ。Text と二重に持つと
                        // 変換側で二重描画になるため Text は空のままにする
                        ParseInline(s.Substring(i + 1, tEnd - i - 1), link);
                        i = uEnd + 1;
                        continue;
                    }
                }

                // 強調。開きは直後が空白でないことが条件。'_' はさらに
                // 直前が英数字でないこと（snake_case を斜体にしないため）
                if ((c == '*' || c == '_') && CanOpen(s, i, c))
                {
                    int run = RunLength(s, i, c);
                    if (run >= 1 && run <= 3)
                    {
                        string mark = new string(c, run);
                        int close = FindClose(s, i + run, mark);
                        if (close > 0)
                        {
                            Flush(sb, parent);
                            string inner = s.Substring(i + run, close - i - run);
                            MdNode host;
                            if (run == 3)
                            {
                                host = parent.Add(new MdNode(MdType.Bold));
                                host = host.Add(new MdNode(MdType.Italic));
                            }
                            else host = parent.Add(new MdNode(run == 2 ? MdType.Bold : MdType.Italic));
                            ParseInline(inner, host);
                            i = close + run;
                            continue;
                        }
                    }
                }

                sb.Append(c);
                i++;
            }
            Flush(sb, parent);

            if (hardBreak) parent.Add(new MdNode(MdType.Break));
        }

        private static bool IsEscapable(char c)
        {
            return c == '*' || c == '_' || c == '`' || c == '[' || c == ']' ||
                   c == '(' || c == ')' || c == '#' || c == '\\' || c == '|' ||
                   c == '!' || c == '>' || c == '<' || c == '~';
        }

        // ===== 強調のフランキング規則（CommonMark の簡易版）=====
        /// <summary>強調の開き記号として使えるか</summary>
        private static bool CanOpen(string s, int i, char c)
        {
            int run = RunLength(s, i, c);
            int after = i + run;
            // 直後が空白・行末なら開きにならない（"a * b" の * を拾わない）
            if (after >= s.Length || s[after] == ' ' || s[after] == '\t') return false;
            // '_' は語中では強調にしない（snake_case_name を守る）
            if (c == '_' && i > 0 && IsWordChar(s[i - 1])) return false;
            return true;
        }

        /// <summary>強調の閉じ記号として使えるか</summary>
        private static bool CanClose(string s, int i, char c)
        {
            // 直前が空白なら閉じにならない（"*a *" のような形を拾わない）
            if (i == 0) return false;
            if (s[i - 1] == ' ' || s[i - 1] == '\t') return false;
            int run = RunLength(s, i, c);
            int after = i + run;
            // '_' は語中では閉じにしない
            if (c == '_' && after < s.Length && IsWordChar(s[after])) return false;
            return true;
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        // ===== 自動リンク =====
        private static bool IsUrl(string s)
        {
            return s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   s.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUrlStart(string s, int i)
        {
            // 単語の途中からは拾わない
            if (i > 0 && IsWordChar(s[i - 1])) return false;
            return string.CompareOrdinal(s, i, "http://", 0, 7) == 0 ||
                   string.CompareOrdinal(s, i, "https://", 0, 8) == 0;
        }

        /// <summary>裸 URL の終端。空白まで進め、文末の句読点は含めない</summary>
        private static int UrlEnd(string s, int i)
        {
            int e = i;
            while (e < s.Length && s[e] != ' ' && s[e] != '\t' && s[e] != '<' && s[e] != '>') e++;
            // 行末に付きやすい記号を落とす
            const string trail = ".,;:!?、。）」』】\"'*_`";
            while (e > i && trail.IndexOf(s[e - 1]) >= 0) e--;
            // 閉じ括弧は URL 内に開き括弧が無いときだけ落とす
            while (e > i && s[e - 1] == ')' && s.IndexOf('(', i) < 0) e--;
            return e;
        }

        /// <summary>ちょうど n 個続くバッククォートの並びを探す</summary>
        private static int FindTickRun(string s, int from, int n)
        {
            for (int k = from; k <= s.Length - n; k++)
            {
                if (s[k] != '`') continue;
                if (RunLength(s, k, '`') != n) { k += RunLength(s, k, '`') - 1; continue; }
                return k;
            }
            return -1;
        }

        private static void Flush(StringBuilder sb, MdNode parent)
        {
            if (sb.Length == 0) return;
            parent.Add(new MdNode(MdType.Text, sb.ToString()));
            sb.Length = 0;
        }

        private static int RunLength(string s, int i, char c)
        {
            int n = 0;
            while (i + n < s.Length && s[i + n] == c) n++;
            return n;
        }

        /// <summary>mark と同じ並びの閉じ位置を探す。見つからなければ -1</summary>
        private static int FindClose(string s, int from, string mark)
        {
            for (int k = from; k <= s.Length - mark.Length; k++)
            {
                if (s[k] == '\\') { k++; continue; }
                if (string.CompareOrdinal(s, k, mark, 0, mark.Length) != 0) continue;
                // mark より長い並びは対象外（*** を ** で閉じない）
                if (RunLength(s, k, mark[0]) != mark.Length) continue;
                // 長い並びの途中（末尾側）にも当てない。"** " を "*" で閉じないため
                if (k > from && s[k - 1] == mark[0]) continue;
                if (k == from) continue;    // 空の強調は認めない
                if (!CanClose(s, k, mark[0])) continue;
                return k;
            }
            return -1;
        }

        /// <summary>s[open] が '[' のとき、"](" と ')' の位置を返す</summary>
        private static bool TryLink(string s, int open, out int textEnd, out int urlEnd)
        {
            textEnd = -1; urlEnd = -1;
            int depth = 0;
            for (int k = open; k < s.Length; k++)
            {
                if (s[k] == '\\') { k++; continue; }
                if (s[k] == '[') depth++;
                else if (s[k] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        if (k + 1 < s.Length && s[k + 1] == '(') { textEnd = k; break; }
                        return false;
                    }
                }
            }
            if (textEnd < 0) return false;

            for (int k = textEnd + 2; k < s.Length; k++)
            {
                if (s[k] == '\\') { k++; continue; }
                if (s[k] == ')') { urlEnd = k; return true; }
            }
            return false;
        }

        // ===== インデント =====
        private static int Indent(string line)
        {
            int n = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == ' ') n++;
                else if (line[i] == '\t') n += 4;
                else break;
            }
            return n;
        }

        private static string Unindent(string line, int width)
        {
            int n = 0, i = 0;
            while (i < line.Length && n < width)
            {
                if (line[i] == ' ') { n++; i++; }
                else if (line[i] == '\t') { n += 4; i++; }
                else break;
            }
            return line.Substring(i);
        }
    }
}
