/*
    File Name : MdTextBlock.cs
    Programmer: Keiji Mitsubuchi

    2026.10.08  created
                リンクを押せる SelectableTextBlock。
                Avalonia 11 には WPF の Hyperlink にあたるインラインが無いので、
                リンクの文字範囲を覚えておき、押された位置を TextLayout で引いて判定する。
                リンクの文字は本文と一緒に折り返され、選んでコピーもできる。
 */
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Media.TextFormatting;

namespace MdAvalonia
{
    public class MdTextBlock : SelectableTextBlock
    {
        /// <summary>テーマは SelectableTextBlock のものをそのまま使う</summary>
        protected override Type StyleKeyOverride
        {
            get { return typeof(SelectableTextBlock); }
        }

        private struct LinkRange
        {
            public int Start;
            public int Length;
            public Uri Uri;
            public string Text;
        }

        private readonly List<LinkRange> _links = new List<LinkRange>();
        private Uri _pressed;

        /// <summary>
        /// 組み立て中の文字位置。MdFlow がインラインを足すたびに進める。
        /// TextLayout が数える位置と合わせる必要がある（Run は文字数、改行は "\n" の 1 文字、
        /// 埋め込み部品（画像）は 1 文字ぶん）。
        /// </summary>
        internal int BuildPosition;

        /// <summary>リンクが押されたときに呼ばれる。null なら OS に開かせる</summary>
        internal Action<Uri> OpenLink;

        internal void AddLink(int start, int length, Uri uri, string text)
        {
            if (uri == null || length <= 0) return;
            _links.Add(new LinkRange { Start = start, Length = length, Uri = uri, Text = text });
        }

        /// <summary>リンクの数（確認用）</summary>
        public int LinkCount
        {
            get { return _links.Count; }
        }

        /// <summary>i 番目のリンクの文字範囲（確認用）</summary>
        public void GetLinkRange(int i, out int start, out int length, out Uri uri)
        {
            start = _links[i].Start;
            length = _links[i].Length;
            uri = _links[i].Uri;
        }

        /// <summary>この点（コントロール内の座標）にあるリンク。無ければ null</summary>
        public Uri LinkAt(Point p)
        {
            if (_links.Count == 0) return null;

            TextLayout layout = TextLayout;
            if (layout == null) return null;

            // HitTestPoint で文字位置を引く方法は、折り返したリンクの最終行で
            // 「範囲外」と返ることがあった（実測）。リンクの文字範囲が占める矩形に
            // 点が入っているかで判定する。リンクの数は少ないので総当たりで足りる
            Thickness pad = Padding;
            var q = new Point(p.X - pad.Left, p.Y - pad.Top);
            for (int i = 0; i < _links.Count; i++)
            {
                LinkRange l = _links[i];
                foreach (Rect r in layout.HitTestTextRange(l.Start, l.Length))
                {
                    if (r.Contains(q)) return l.Uri;
                }
            }
            return null;
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (_links.Count == 0) return;

            Uri uri = LinkAt(e.GetPosition(this));
            Cursor = uri != null ? new Cursor(StandardCursorType.Hand) : null;
            ToolTip.SetTip(this, uri != null ? uri.OriginalString : null);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            if (_links.Count == 0) return;
            Cursor = null;
            ToolTip.SetTip(this, null);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            _pressed = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
                ? LinkAt(e.GetPosition(this))
                : null;
            base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            Uri pressed = _pressed;
            _pressed = null;
            if (pressed == null || e.InitialPressMouseButton != MouseButton.Left) return;

            // 文字を選ぶつもりでリンクの上からドラッグしたときは開かない
            if (SelectionStart != SelectionEnd) return;

            Uri released = LinkAt(e.GetPosition(this));
            if (released == null || released != pressed) return;

            e.Handled = true;
            Open(pressed);
        }

        private void Open(Uri uri)
        {
            if (OpenLink != null)
            {
                OpenLink(uri);
                return;
            }

            try
            {
                TopLevel top = TopLevel.GetTopLevel(this);
                if (top == null) return;
                if (uri.IsFile)
                    top.Launcher.LaunchFileInfoAsync(new System.IO.FileInfo(uri.LocalPath));
                else
                    top.Launcher.LaunchUriAsync(uri);
            }
            catch { /* 既定のブラウザが無い等は無視 */ }
        }
    }
}
