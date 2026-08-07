# MdViewer

Markdown と SVG を表示する WPF ライブラリと、閲覧専用ビューアアプリ。

- **Markdown パーサは自前実装・外部依存なし**。`netstandard2.0` なので Unity / .NET 8 /
  コンソールからも使える
- SVG の解析は [SharpVectors](https://github.com/ElinamLLC/SharpVectors) に委譲
- Markdown 内に埋め込んだ SVG 画像も描画される

Programmer : Keiji Mitsubuchi / Virtual IP Production

---

## 構成

| プロジェクト | ターゲット | 内容 | 外部依存 |
|---|---|---|---|
| **MdLib** | `netstandard2.0` | Markdown 解析（`MdNode` / `MdParser`） | なし |
| **MdWpf** | `net472` + WPF | FlowDocument 生成と `MdViewer` コントロール | なし |
| **SvgWpf** | `net472` + WPF | `SvgRender` / `SvgViewer` コントロール | SharpVectors 1.8.4 |
| **MdViewerApp** | `net472` + WPF | 閲覧専用ビューア（タブ・拡大縮小） | — |
| TestWpf | `net472` + WPF | ライブラリ開発用のテスト台 | — |

```
netstandard2.0 ┃ MdLib ──────────────┐
───────────────╂─────────────────────┼──────
net472 + WPF   ┃ SvgWpf ── SharpVectors
               ┃    └──────────── MdWpf
```

**解析（MdLib）と描画（MdWpf）を分離しています。** WPF は .NET Standard 2.0 に含まれない
ため、描画側だけが Windows デスクトップ専用です。Markdown を解析するだけなら `MdLib`
単体で足ります。

C# は全プロジェクト `LangVersion 7.3`（Unity 対応のため）。

このソリューションは自己完結しています。外部依存は SharpVectors だけで、他の自作
ライブラリには一切依存していません。`MdViewer.sln` を開けばそのままビルドできます。

---

## ビルド

```bash
dotnet build MdViewer.sln
```

Visual Studio 2022 で `MdViewer.sln` を開いてビルドしても同じです。
初回は NuGet の復元が必要です（SharpVectors）。

---

## MdViewerApp — 閲覧専用ビューア

`MdViewerApp/bin/Debug/net472/MdViewerApp.exe`

**表示専用です。編集はできません。**

| 操作 | 方法 |
|---|---|
| 開く | メニュー ファイル ▸ 開く / `Ctrl+O` / ウィンドウへドラッグ＆ドロップ |
| 閉じる | メニュー ファイル ▸ 閉じる / `Ctrl+W` / タブの ✕ |
| タブ切り替え | タブをクリック |
| 拡大 | メニュー 表示 ▸ 拡大 / `Ctrl++` / `Ctrl+マウスホイール` / ステータスバーの ＋ |
| 縮小 | メニュー 表示 ▸ 縮小 / `Ctrl+-` / `Ctrl+マウスホイール` / ステータスバーの － |
| 標準に戻す | メニュー 表示 ▸ 標準に戻す / ステータスバーの「標準」 |

- 複数ファイルを同時に開けます（ダイアログで複数選択、またはまとめてドロップ）
- 同じファイルを再度開くと、新しいタブを作らず既存のタブに切り替わります
- 倍率はタブごとに独立しています
- `.md` / `.markdown` は `MdViewer`、`.svg` は `SvgViewer` で表示します
- Markdown 内の画像は相対パスで解決されます
- コマンドライン引数でファイルを渡すと起動時に開きます

```bash
MdViewerApp.exe C:\docs\report.md C:\docs\chart.svg
```

`MdViewerApp/Samples/` に SVG を埋め込んだ Markdown のサンプルがあります。

倍率の段階は 50 / 67 / 80 / 90 / 100 / 110 / 125 / 150 / 175 / 200 / 250 / 300 / 400 ％です。

---

## MdLib — 解析のみ（WPF 不要）

```csharp
using MdLib;

MdNode doc = MdParser.Parse(markdownText);

int blocks = doc.Kids.Count;        // ブロック要素の数
string plain = doc.PlainText();     // 装飾を外したテキスト
string dump = doc.Dump();           // ツリーをインデント表示（デバッグ用）

foreach (MdNode block in doc.Kids)
{
    if (block.Type == MdType.Heading)
        Console.WriteLine(new string('#', block.Level) + " " + block.PlainText());
}

// 1 行分のインライン要素だけ解析する
var line = new MdNode(MdType.Paragraph);
MdParser.ParseInline("**太字**と`コード`", line);
```

`MdNode` は種別ごとにクラスを分けず、`MdType` 列挙 1 本でブロックとインラインを扱います。

| フィールド | 意味 |
|---|---|
| `Type` | 種別（`MdType` 列挙） |
| `Text` | `Text` / `Code` / `CodeBlock` の中身。`Image` では alt |
| `Url` | `Link` / `Image` の URL。`CodeBlock` では言語名 |
| `Level` | `Heading` の 1..6。`ListItem` ではインデント幅 |
| `Header` | `TableRow` がヘッダ行か |
| `Task` / `Checked` | `ListItem` がタスクリストか、チェック済みか |
| `Align` | `TableCell` の揃え |
| `Kids` | 子ノード |

---

## MdWpf — WPF 描画

### コントロールとして貼る

```xml
<Window xmlns:md="clr-namespace:MdWpf;assembly=MdWpf">
    <md:MdViewer x:Name="mdViewer" />
</Window>
```

```csharp
mdViewer.LoadFile(path);      // Markdown / BasePath をまとめて設定
mdViewer.Zoom = 150;          // 表示倍率（％）。100 で等倍
MdNode tree = mdViewer.Tree;  // 直近の解析結果
```

### FlowDocument に変換する

```csharp
using MdLib;
using MdWpf;

FlowDocument doc = MdFlow.ToFlowDocument(markdownText);
flowDocumentScrollViewer.Document = doc;

// 見た目を変える
var style = new MdStyle
{
    FontSize = 16,
    FontFamily = new FontFamily("Yu Gothic UI"),
    ImageMaxWidth = 800,
    BasePath = @"C:\docs",     // 相対パスの画像を解決する基準
};
FlowDocument doc2 = MdFlow.ToFlowDocument(markdownText, style);

// 解析済みの木から変換する（解析を 1 回で済ませたい場合）
MdNode tree = MdParser.Parse(markdownText);
FlowDocument doc3 = MdFlow.ToFlowDocument(tree, style);
```

`MdStyle` でフォント・文字サイズ・見出しサイズ・各色・余白・画像の最大幅を変更できます。

---

## SvgWpf — SVG

### コントロールとして貼る

```xml
<Window xmlns:svg="clr-namespace:SvgWpf;assembly=SvgWpf">
    <svg:SvgViewer Source="C:\data\icon.svg" Stretch="Uniform" />
</Window>
```

```csharp
svgViewer.Source  = @"C:\data\icon.svg";   // 先頭が '<' なら XML 文字列として扱う
svgViewer.SvgText = svgXmlString;          // XML を明示的に渡す
svgViewer.Zoom    = 200;                   // 表示倍率（％）
bool ok = svgViewer.IsSvgLoaded;
Rect size = svgViewer.SvgBounds;
```

`Zoom` が 100 のときは `Stretch` に従って枠に合わせます。100 以外のときは原寸 × 倍率で
描き、はみ出た分は内部の `ScrollViewer` でスクロールします。

### ImageSource に変換する

```csharp
using SvgWpf;

image.Source = SvgRender.FromFile(@"C:\data\icon.svg");
image.Source = SvgRender.FromText(svgXmlString);
image.Source = SvgRender.FromStream(stream);
image.Source = SvgRender.FromUri(new Uri("pack://application:,,,/icon.svg"));
image.Source = SvgRender.From(fileOrXml);        // どちらでも受け取る

DrawingGroup g = SvgRender.GroupFromFile(path);  // 図形として加工したい場合
```

**失敗時は例外を投げず `null` を返します。** 呼び出し側で null チェックしてください。

---

## WinForms から使う

`ElementHost` を挟めば WinForms からも使えます。

```csharp
using System.Windows.Forms.Integration;
using MdWpf;

var viewer = new MdViewer { Markdown = text };
var host = new ElementHost { Dock = DockStyle.Fill, Child = viewer };
panel1.Controls.Add(host);
```

呼び出し側に `WindowsFormsIntegration` / `PresentationCore` / `PresentationFramework` /
`WindowsBase` / `System.Xaml` の参照が必要です。

旧形式 csproj（packages.config 系）から使う場合、**推移的な `ProjectReference` は効きません。**
`MdNode` を使うなら `MdLib` も明示的に参照してください。

---

## Markdown の対応範囲

### 対応している記法

**ブロック**

| 記法 | 例 |
|---|---|
| 見出し | `#` 〜 `######` |
| コードブロック | ` ``` ` で囲む（言語名の指定可）／4 スペース・タブのインデント |
| 引用 | `>`（入れ子可、中にブロック要素を書ける） |
| 箇条書き | `-` `*` `+`（インデントで入れ子、項目内にコードブロックも可） |
| 番号付きリスト | `1.` `2.`（同上） |
| タスクリスト | `- [ ]` / `- [x]` → ☐ / ☑ で表示 |
| 水平線 | `---` `***` `___`（3 個以上） |
| 表 | `\| a \| b \|` ＋ `\|---\|:--:\|--:\|`（左・中央・右揃え） |

**インライン**

| 記法 | 例 |
|---|---|
| コード | `` `code` ``。バッククォートを含めたい場合は 2 本以上で囲む |
| 強調 | `**bold**` `__bold__` `*italic*` `_italic_` `***both***` |
| 打ち消し線 | `~~text~~` |
| リンク | `[text](url)` |
| 自動リンク | `<https://...>`、裸の `https://...` |
| 画像 | `![alt](url)`（`.svg` も表示可。読めない場合は alt テキストで代替） |
| 改行 | 行末に半角スペース 2 個 |
| エスケープ | `` \* \_ \` \[ \] \( \) \# \\ \| \! \> \< \~ `` |

強調は CommonMark の簡易フランキング規則に従います。`snake_case_name` や `a * b * c` が
斜体にならないよう、`_` は語中で無効、開き記号の直後が空白の場合も無効です。

### 対応していない記法

- 参照リンク（`[text][ref]` ＋ `[ref]: url`）
- HTML の直接埋め込み（`<div>` など）
- setext 見出し（`===` `---` による下線形式）
- `~~~` によるコードフェンス（` ``` ` は対応）
- 表セル内での改行

CommonMark 完全準拠ではありません。

---

## 設計メモ

なぜこうなっているかの記録です。改造するときの参考にしてください。

### 解析と描画を分離している

**`MdLib` は WPF に一切依存しません**（参照アセンブリは `netstandard` のみ）。
そのため Unity / .NET 8 / コンソール / ASP.NET Core からも Markdown 解析だけ使えます。
.NET 8 のコンソールアプリで動作確認済みです。

WPF は .NET Standard 2.0 に含まれないため、描画側（`MdWpf` / `SvgWpf`）は `net472` です。
`.NET 5` 以降から使いたくなったら、csproj を
`<TargetFrameworks>net472;net8.0-windows</TargetFrameworks>` にすればコード変更なしで通ります。

**この分離は壊さないでください。** `MdLib` に `System.Windows.*` を持ち込むと、
Unity やコンソールから使えなくなります。

### MdNode は 1 クラスで全種別を表す

種別ごとにクラスを分けず、`MdType` 列挙 1 本でブロックとインラインを扱っています。
継承階層を作るより、`MdFlow` 側を単純な `switch` で書けるほうが読みやすいという判断です。
`MdNode.Dump()` で木をインデント表示できるので、パーサの挙動を追うときに使ってください。

### SVG は SharpVectors に委譲している

SVG は仕様が大きいので自前実装していません。`SvgRender` は薄いラッパで、
**失敗時に例外を投げず `null` を返します**。呼び出し側で null チェックする方針です。

Markdown 内の `.svg` 画像は `MdFlow.Image()` から `SvgRender.FromUri()` を呼んで描画しています。
これが `MdWpf` → `SvgWpf` 依存の唯一の理由で、該当箇所は 1 か所だけです。
SVG を切り離したい場合はここをデリゲート差し替えにしてください。

### Zoom の意味が Markdown と SVG で違う

- `MdViewer.Zoom` — `FlowDocumentScrollViewer.Zoom`（％）。100 で等倍
- `SvgViewer.Zoom` — 100 のときは `Stretch` に従って**枠に合わせる**。100 以外は原寸 × 倍率で
  描き、はみ出しは内部 `ScrollViewer` でスクロール

そのため `MdViewerApp` の「標準に戻す」は両方に 100 をセットしますが、Markdown では 100%、
SVG ではフィット表示になります。ラベルが「100%」ではなく「標準に戻す」なのはこのためです。

---

## 実装メモ — ハマった箇所

同じ罠を踏み直さないための記録です。

### Span.TextDecorations は子の Run に伝わらない

打ち消し線（`~~text~~`）が、解析はできているのに線が描かれませんでした。
`Span` に `TextDecorations` を設定しても子の `Run` には継承されません。
`MdFlow.SetStrike()` で `Run` まで再帰的に辿り、個別に設定しています。
`Bold` / `Italic` / `Hyperlink` はいずれも `Span` 派生なので、同じ再帰で拾えます。

### 水平線は空 Paragraph の罫線では潰れる

`---` を `FontSize=0.1` の空 `Paragraph` に上罫線を引く方式では、行高で潰れて見えませんでした。
高さ 1 の `Border` を `BlockUIContainer` に載せる方式にしています。

### StatusBar の DockPanel は最後の子が残り幅を占める

`StatusBar.ItemsPanel` に `DockPanel` を使う場合、`Dock="Right"` の項目を
**残り幅を占める項目より先**に書かないと右側が切れます。
`MdViewerApp/MainWindow.xaml` では拡大縮小パネルを先、パス表示を後にしています。

### リスト項目内のブロック要素

番号付きリストの途中にコードブロックを挟むと、リストが分断されて番号が振り直される問題が
ありました。`MdParser.ParseList()` は項目の続き行をまとめて集め、字下げを外してから
`ParseBlocks` に再帰させる方式にしてあります。
入れ子リスト・項目内コード・項目内引用がこれで一括して扱えます。

### 強調のフランキング規則

`_ref` や `snake_case_name`、`a * b * c` がコードブロック外に生で出たときに斜体化しないよう、
CommonMark の簡易フランキング規則を入れています（`CanOpen` / `CanClose`）。
`_` は語中で無効、開き記号の直後が空白なら無効です。
**ここを緩めると識別子が壊れます。**

---

## 動作確認のやり方

GUI を目で見ずに検証したい場合は、描画結果を PNG に落とすのが確実です。
`RenderTargetBitmap` で `MdViewer` / `SvgViewer` をラスタライズする小さな STA コンソール
アプリを作り、PNG を吐かせて画像として確認します。要点は 3 つです。

- `Main` に `[STAThread]` が必要
- `Measure` → `Arrange` → `UpdateLayout` の後、**Dispatcher を回さないと FlowDocument の
  レイアウトが確定しません**（`DispatcherFrame` ＋ `DispatcherPriority.SystemIdle` で pump する）
- `OutputType` は `Exe`。`WinExe` だとコンソール出力が消えて失敗が見えなくなります

`MdViewerApp` 自体の検証は、リフレクションで `MainWindow` の private フィールド
（`tabs` / `textStatus` / `textZoom` / `textEmpty`）とメソッド（`OpenFiles` / `CloseTab` /
`StepZoom` / `SetZoom`）を叩いて行いました。

PowerShell から exe を呼ぶときの注意: **空文字列の引数は落とされて引数がずれます。**
プレースホルダ（`"-"` など）を渡してください。

---

## 配布時に必要な DLL

自作 3 つ（約 45 KB）と SharpVectors 6 つ（約 1.8 MB）の計 9 個です。

```
MdLib.dll  MdWpf.dll  SvgWpf.dll
SharpVectors.Model.dll  SharpVectors.Rendering.Wpf.dll  SharpVectors.Converters.Wpf.dll
SharpVectors.Core.dll   SharpVectors.Css.dll            SharpVectors.Dom.dll
SharpVectors.Runtime.Wpf.dll
```

`SharpVectors.Rendering.Gdi.dll` は NuGet が同梱しますが参照チェーンのどこからも使われていないため、
配布時は除外できます（削除した構成で動作確認済み）。

---

## 既知の注意点

- `MdFlow.ToFlowDocument()` は WPF の Dispatcher が必要です。UI スレッドから呼んでください
- `SvgRender.From*()` の戻り値は `Freeze()` 済みなので、別スレッドから UI に渡せます
- コンソールアプリから使う場合、`Main` に `[STAThread]` が必要です
- リンクのクリックは既定のブラウザで開きます（`Process.Start`）

---

## ライセンス

[MIT License](LICENSE) — Copyright (c) 2026 Keiji Mitsubuchi

SVG の解析に使っている [SharpVectors](https://github.com/ElinamLLC/SharpVectors) は
BSD-3-Clause（Copyright (c) 2010 - 2024 Elinam LLC）です。SharpVectors の DLL を同梱して
再配布する場合は、そちらの著作権表示とライセンス条文も併せて添付してください。

`MdLib` は外部依存がないため、Markdown 解析だけを使う場合は MIT のみで完結します。
