using System.Collections.Generic;
using System.Text;

namespace MdLib
{
    /// <summary>ノード種別。ブロック要素とインライン要素を 1 つの enum で扱う</summary>
    public enum MdType
    {
        Document,
        // ブロック
        Heading, Paragraph, CodeBlock, BulletList, NumberList, ListItem,
        Quote, Rule, Table, TableRow, TableCell,
        // インライン
        Text, Bold, Italic, Strike, Code, Link, Image, Break
    }

    public enum MdAlign { Left, Center, Right }

    /// <summary>
    /// Markdown の解析結果を表す木のノード。
    /// 種別ごとにクラスを分けず 1 クラスで持つことで、
    /// FlowDocument への変換側を単純な switch で書けるようにしている。
    /// </summary>
    public class MdNode
    {
        public MdType Type;

        /// <summary>Text / Code / CodeBlock の中身。Link / Image では表示文字（alt）</summary>
        public string Text = "";

        /// <summary>Link / Image の URL。CodeBlock では言語名</summary>
        public string Url = "";

        /// <summary>Heading の 1..6。ListItem / Quote ではネストの深さ</summary>
        public int Level;

        /// <summary>TableRow がヘッダ行かどうか</summary>
        public bool Header;

        /// <summary>ListItem がタスクリスト（- [ ] / - [x]）かどうか</summary>
        public bool Task;

        /// <summary>Task が true のとき、チェック済みかどうか</summary>
        public bool Checked;

        /// <summary>TableCell の揃え</summary>
        public MdAlign Align = MdAlign.Left;

        public readonly List<MdNode> Kids = new List<MdNode>();

        public MdNode(MdType type) { Type = type; }

        public MdNode(MdType type, string text) { Type = type; Text = text; }

        /// <summary>子を追加して、その子を返す</summary>
        public MdNode Add(MdNode kid)
        {
            Kids.Add(kid);
            return kid;
        }

        public bool HasKids => Kids.Count > 0;

        /// <summary>この節以下のテキストを連結する（プレーンテキスト化）</summary>
        public string PlainText()
        {
            var sb = new StringBuilder();
            Plain(this, sb);
            return sb.ToString();
        }

        private static void Plain(MdNode n, StringBuilder sb)
        {
            if (n.Type == MdType.Text || n.Type == MdType.Code || n.Type == MdType.CodeBlock)
                sb.Append(n.Text);
            if (n.Type == MdType.Break) sb.Append("\n");
            for (int i = 0; i < n.Kids.Count; i++) Plain(n.Kids[i], sb);
        }

        /// <summary>木構造をインデント付きで表示（デバッグ用）</summary>
        public string Dump(int depth = 0)
        {
            var sb = new StringBuilder();
            sb.Append(new string(' ', depth * 2));
            sb.Append(Type.ToString());
            if (Level != 0) sb.Append(" L" + Level);
            if (Header) sb.Append(" [header]");
            if (Task) sb.Append(Checked ? " [x]" : " [ ]");
            if (Align != MdAlign.Left) sb.Append(" " + Align);
            if (Url != "") sb.Append(" url=\"" + Url + "\"");
            if (Text != "") sb.Append(" \"" + Text.Replace("\n", "\\n") + "\"");
            sb.Append("\r\n");
            for (int i = 0; i < Kids.Count; i++) sb.Append(Kids[i].Dump(depth + 1));
            return sb.ToString();
        }
    }
}
