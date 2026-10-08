# MdViewer

Markdown と SVG、画像・動画を表示する WPF / Avalonia ライブラリと、閲覧専用ビューアアプリ。

- **Markdown パーサは自前実装・外部依存なし**。`netstandard2.0` なので Unity / .NET 8 /
  コンソールからも使える
- SVG の解析は [SharpVectors](https://github.com/ElinamLLC/SharpVectors) に委譲
- Markdown 内に埋め込んだ SVG 画像も描画される
- **動画は Windows のサムネイル（ポスターフレーム）で表示する。** 外部ツールは不要
- **Avalonia 版もある**（`MdAvalonia` / `SvgAvalonia` / `ImgAvalonia`）。Windows / macOS / Linux で動く。
  解析は WPF 版と同じ `MdLib` を使う

Programmer : Keiji Mitsubuchi / Virtual IP Production

---

## 構成

| プロジェクト | ターゲット | 内容 | 外部依存 |
|---|---|---|---|
| **MdLib** | `netstandard2.0` | Markdown 解析（`MdNode` / `MdParser`） | なし |
| **MdWpf** | `net472` / `net9.0-windows` | FlowDocument 生成と `MdViewer` コントロール | なし |
| **SvgWpf** | `net472` / `net9.0-windows` | `SvgRender` / `SvgViewer` コントロール | SharpVectors 1.8.4 |
| **ImgWpf** | `net472` / `net9.0-windows` | `ImgRender` / `ImgViewer` コントロール。画像と動画サムネイル | なし（OS の機能） |
| **MdViewerApp** | `net472` + WPF | 閲覧専用ビューア（タブ・拡大縮小） | — |
| TestWpf | `net472` + WPF | ライブラリ開発用のテスト台 | — |
| **MdAvalonia** | `netstandard2.0` | Avalonia 版の描画と `MdViewer` コントロール | Avalonia 11.3 |
| **SvgAvalonia** | `netstandard2.0` | Avalonia 版の `SvgRender` / `SvgViewer` | Avalonia.Svg.Skia 11.3.0 |
| **ImgAvalonia** | `netstandard2.0` | Avalonia 版の `ImgRender` / `ImgViewer` | SkiaSharp 3.116.1 |
| TestAvalonia | `net9.0` + Avalonia | Avalonia 版のテスト台 | — |

```
netstandard2.0 ┃ MdLib ───────────────────┐
───────────────╂──────────────────────────┼──────
net472 および  ┃ SvgWpf ── SharpVectors    │
net9.0-windows ┃    └───────────────── MdWpf
               ┃ ImgWpf ──────────────────┤
───────────────╂──────────────────────────┼──────
netstandard2.0 ┃ SvgAvalonia ── Svg.Skia   │
（Avalonia）   ┃    └───────────────── MdAvalonia
               ┃ ImgAvalonia ── SkiaSharp ─┘
```

**解析（MdLib）と描画（MdWpf）を分離しています。** WPF は .NET Standard 2.0 に含まれない
ため、描画側だけが Windows デスクトップ専用です。Markdown を解析するだけなら `MdLib`
単体で足ります。

C# は全プロジェクト `LangVersion 7.3`（Unity 対応のため）。

このソリューションは自己完結しています。外部依存は SharpVectors（WPF 版）と
Avalonia・Svg.Skia・SkiaSharp（Avalonia 版）だけで、他の自作ライブラリには一切依存していません。
`MdViewer.sln` を開けばそのままビルドできます。

---

## ビルド

```bash
dotnet build MdViewer.sln
```

Visual Studio 2022 で `MdViewer.sln` を開いてビルドしても同じです。
初回は NuGet の復元が必要です（SharpVectors、Avalonia ほか）。

Avalonia 版のテスト台は、どの OS でも次で起動できます（引数にファイルを渡すと開く）。

```bash
dotnet run --project TestAvalonia -- MdViewerApp/Samples/report.md
```

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
- Markdown 内の画像は相対パスで解決されます
- コマンドライン引数でファイルを渡すと起動時に開きます

拡張子で表示するコントロールを決めます。

| 拡張子 | コントロール |
|---|---|
| `.md` `.markdown`、および下記以外 | `MdViewer` |
| `.svg` | `SvgViewer` |
| `.png` `.jpg` `.gif` `.bmp` `.tif` `.ico` `.webp` `.heic` など | `ImgViewer` |
| `.mp4` `.mov` `.avi` `.wmv` `.mkv` `.webm` `.mpg` など | `ImgViewer`（サムネイル） |

動画はサムネイル（ポスターフレーム）の静止画を表示します。**再生はできません。**

```bash
MdViewerApp.exe C:\docs\report.md C:\docs\chart.svg
```

`MdViewerApp/Samples/` に SVG を埋め込んだ Markdown のサンプルがあります。
`Samples/SvgCharts/charts.md` は、グラフ SVG の対応範囲を実例で確かめられるサンプルです。

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

### SVG の対応範囲（グラフを描く場合）

解析は SharpVectors に任せているため、グラフ SVG でよく使われる書き方はひととおり通ります。
`MdViewerApp/Samples/SvgCharts/` に実例を 10 個入れてあります。`charts.md` を開くと
Markdown に埋め込んだ状態で全部まとめて見られます。

| 書き方 | 対応 |
|---|---|
| `rect` `line` `circle` `path`（円弧 `A` コマンド含む） | 対応 |
| `text` / `tspan` / `textPath` / `text-anchor` / `letter-spacing` | 対応 |
| `linearGradient` / `stroke-dasharray` / `clipPath` | 対応 |
| `defs` / `use` / `symbol` / `marker`（軸の矢印など） | 対応 |
| 入れ子の `g transform`（`translate` / `rotate` / `scale`） | 対応 |
| `<style>` の CSS クラス指定 | 対応 |
| 日本語のラベル・タイトル | 対応 |
| `filter`（影・ぼかし） | **非対応** |
| `foreignObject`（HTML 埋め込み） | **非対応** |
| SMIL `animate` | 静止画として描画（動かない） |

非対応の 2 つはどちらも**部分的な欠落**です。`filter` は効果が消えるだけで図形自体は
描かれますし、`foreignObject` も枠が残るだけで同じ SVG 内の `text` は描かれます。
読み込みに失敗するわけではありません。

**Mermaid を使う場合の注意。** Mermaid は既定でノードのラベルを `foreignObject` に入れるため、
そのままだと図形は出るのに文字だけ消えます。Mermaid 側で `htmlLabels: false` を指定して
`text` 要素で書き出させてください。matplotlib や Excel、Illustrator の SVG 書き出しは
`text` を使うので影響ありません。

---

## ImgWpf — 画像と動画サムネイル

外部依存はありません。画像のデコードは WPF 標準の WIC、動画のサムネイルは
Windows Shell の `IShellItemImageFactory` を使います。どちらも OS の機能なので、
ffmpeg のような外部ツールを抱える必要がありません。

### コントロールとして貼る

```xml
<Window xmlns:img="clr-namespace:ImgWpf;assembly=ImgWpf">
    <img:ImgViewer Source="C:\data\photo.jpg" />
</Window>
```

```csharp
imgViewer.Source = @"C:\data\clip.mp4";   // 動画ならサムネイルを表示する
imgViewer.Zoom   = 200;                   // 表示倍率（％）
bool ok    = imgViewer.IsImageLoaded;
bool thumb = imgViewer.IsThumbnail;       // サムネイルで代替表示しているか
Size px    = imgViewer.PixelSize;         // 表示している画像の画素サイズ
```

`Zoom` の意味は `SvgViewer` と同じです。100 のときは `Stretch` に従って枠に合わせ、
100 以外のときは原寸 × 倍率で描いて、はみ出た分は内部の `ScrollViewer` でスクロールします。

### BitmapSource に変換する

```csharp
using ImgWpf;

// 中身に応じて自動で振り分ける（画像はデコード、動画はサムネイル）
image.Source = ImgRender.From(@"C:\data\photo.jpg", 600);
image.Source = ImgRender.From(@"C:\data\clip.mp4");

// 明示的に指定する
image.Source = ImgRender.FromFile(@"C:\data\photo.jpg", 600);   // 600 px へ縮小してデコード
image.Source = ImgRender.FromStream(stream);
image.Source = ImgRender.FromUri(new Uri("https://example.com/a.png"));
image.Source = ImgRender.ThumbnailFromFile(@"C:\data\clip.mp4", 512);

int w = ImgRender.PixelWidthOf(path);        // ヘッダだけ読んで原寸の横幅を得る
bool v = ImgRender.IsVideoFile(path);
bool s = ImgRender.IsSupported(path);        // ImgWpf が表示を試みる拡張子か
```

**`SvgRender` と同じく、失敗時は例外を投げず `null` を返します。**

### デコード時に縮小する

`FromFile` / `From` の第 2 引数に幅を渡すと、**デコードの時点で**その幅まで縮小します
（`DecodePixelWidth`）。読み込んでから縮小するのではないので、大きな写真でメモリと時間を
節約できます。原寸より大きい値を渡しても拡大はしません。

```csharp
// 6000 x 4000 の写真を 600 px 相当で読む。全画素は展開されない
image.Source = ImgRender.FromFile(photo, 600);
```

`FromUri` は原寸が分からないため縮小指定を受け付けません。表示側の `MaxWidth` で
合わせてください。

### サムネイルについて

- エクスプローラと同じサムネイルが返り、OS のサムネイルキャッシュに乗ります。
  実測で初回 49 ms、2 回目 10 ms 程度でした（mp4、512 px）
- **COM を使うので STA スレッドから呼んでください**（WPF の UI スレッドは STA）
- サムネイルを持たないファイルは `null` を返します。第 3 引数に `true` を渡すと
  種類アイコン（白紙アイコンなど）で代替します。既定は `false` です
- 取得できるかは OS のサムネイルハンドラ次第です。`.mp4` `.wmv` `.avi` は標準で取れます。
  `.mkv` などは対応する拡張機能が必要な場合があります

---

## Avalonia 版 — MdAvalonia / SvgAvalonia / ImgAvalonia

WPF 版と同じ名前・同じ使い方にしてあります（名前空間だけが違う）。Windows / macOS / Linux で動きます。

```csharp
using MdAvalonia;

var viewer = new MdViewer();
viewer.LoadFile(path);          // BasePath も設定され、相対パスの画像を解決する
viewer.Zoom = 150;              // 20〜200（％）

Control view = MdFlow.ToControl(markdownText);   // コントロールに変換するだけ
```

```csharp
using SvgAvalonia;   // new SvgViewer { Source = path } / SvgRender.FromFile(path) → IImage
using ImgAvalonia;   // new ImgViewer { Source = path } / ImgRender.From(path, 600) → Bitmap
```

### WPF 版との違い

| 項目 | WPF 版 | Avalonia 版 |
|---|---|---|
| Markdown の描画 | FlowDocument | ブロックごとの部品（`MdTextBlock` など）を縦に並べる |
| 文字の選択 | 文書全体をまたいで選べる | **段落・セルの中でだけ選べる** |
| リンク | `Hyperlink` | 文字範囲を覚えて当たり判定する（折り返したリンクも押せる） |
| リンクを押したとき | `Process.Start` | `TopLevel.Launcher`。`MdStyle.OpenLink` で差し替えられる |
| SVG の大きさ | 描かれた範囲に詰める | **SVG の width / height のとおり**（余白も含む） |
| 画像の幅 | `ImageMaxWidth` まで | `ImageMaxWidth` と**表示幅の小さいほう**（狭い欄でははみ出さず縮む） |
| http の画像 | 読み込みは WPF 任せ | 取りに行く間は alt を出し、届いたら差し替える |
| tif / heic など | WIC（OS のコーデック） | SkiaSharp で読めない形式は、Windows ではサムネイルで代わりに出す |
| 動画 | Windows のサムネイル | 同じ。**Windows 以外では出ない**（`null`） |

EXIF の向きは SkiaSharp が読んだ値を当てています（8 通りとも実測で確認）。

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
| 画像 | `![alt](url)` |
| 動画 | `![alt](clip.mp4)` — サムネイルを表示する |
| 改行 | 行末に半角スペース 2 個 |
| エスケープ | `` \* \_ \` \[ \] \( \) \# \\ \| \! \> \< \~ `` |

強調は CommonMark の簡易フランキング規則に従います。`snake_case_name` や `a * b * c` が
斜体にならないよう、`_` は語中で無効、開き記号の直後が空白の場合も無効です。

画像の記法は 1 つですが、拡張子で描き方が変わります。`.svg` は SvgWpf、それ以外の
ローカル画像と動画は ImgWpf が担当します。`http://` などは `Uri` のまま読み込みます。
**読めない画像・動画は alt テキストで代替します**（`[alt]` と表示）。
大きな画像は `MdStyle.ImageMaxWidth` × `ImageDecodeScale` の幅でデコードされます。

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

WPF は .NET Standard 2.0 に含まれないため、描画側（`MdWpf` / `SvgWpf` / `ImgWpf`）は
Windows デスクトップ専用です。**`net472` と `net9.0-windows` の両方に出しています**
（ソースは共通、コード変更なし）。`net472` は Unity や既存の .NET Framework アプリ向け、
`net9.0-windows` は .NET 5 以降のアプリ向けです。SharpVectors は `net8.0-windows7.0` の
アセンブリが使われます。

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

### 動画サムネイルは Windows Shell に委譲している

動画のフレームを自前でデコードすると、コーデックの塊（ffmpeg など）を抱えることになります。
`ImgRender` は Windows Shell の `IShellItemImageFactory::GetImage` を呼び、
**エクスプローラが表示しているのと同じサムネイル**をもらう方式にしました。

- 追加の DLL が 1 つも増えない（SharpVectors だけという方針を崩さない）
- OS のサムネイルキャッシュに乗るので 2 回目以降が速い
- 動画に限らず、サムネイルハンドラがある形式（PDF、Office 文書など）も同じ経路で出る

`MediaPlayer` ＋ `RenderTargetBitmap` でフレームを抜く手もありますが、`MediaOpened` 待ちの
非同期処理になり、同期的な `MdFlow.ToFlowDocument()` と噛み合わないため採っていません。

### Zoom の意味が Markdown と画像・SVG で違う

- `MdViewer.Zoom` — `FlowDocumentScrollViewer.Zoom`（％）。100 で等倍
- `SvgViewer.Zoom` / `ImgViewer.Zoom` — 100 のときは `Stretch` に従って**枠に合わせる**。
  100 以外は原寸 × 倍率で描き、はみ出しは内部 `ScrollViewer` でスクロール

そのため `MdViewerApp` の「標準に戻す」は 100 をセットしますが、Markdown では 100%、
SVG と画像ではフィット表示になります。ラベルが「100%」ではなく「標準に戻す」なのは
このためです。

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

### ScrollViewer の中では Stretch が効かない

`SvgViewer` / `ImgViewer` は拡大時のスクロールのために `Image` を `ScrollViewer` に入れて
いますが、**`ScrollViewer` は中身を無限の大きさで測る**ため、`Stretch="Uniform"` を指定しても
枠に合わせてくれず原寸のまま描かれてしまいます（2000 px の画像が 700 px の枠からはみ出す）。

枠に合わせるときは `ScrollBarVisibility.Disabled` にしてスクロールを切ります。こうすると
`ScrollViewer` が枠の大きさを中身に伝えるので `Stretch` が効きます。100 % 以外のときだけ
`Auto` に戻します。`Stretch.None` は「原寸で見る」指定なので、100 % でも枠には合わせません。

### HBITMAP からの変換で α が落ちる

Shell から返る HBITMAP を `Imaging.CreateBitmapSourceFromHBitmap()` で変換すると
**α が落ちて透明部分が黒くなります**。`GetDIBits` で自分で読み、32bpp なら `Pbgra32`
（Shell が返すのは乗算済み α）として `BitmapSource` を作っています。

24bpp 以下のときに `Pbgra32` にしてはいけません。`GetDIBits` が α のバイトに 0 を書くため、
**画像全体が完全に透明になって何も見えなくなります**。この場合は α を見ない `Bgr32` を使います。

### サムネイルの「アイコン代替」は既定で切る

`IShellItemImageFactory::GetImage` は既定で、サムネイルが無いファイルに種類アイコンを返します。
これを有効にしたままだと、**壊れた画像が「白紙アイコン」として表示されて失敗に気づけません**。
Markdown 中の壊れた画像が巨大な白紙アイコンになる不具合が実際に出たため、
`SIIGBF_THUMBNAILONLY` を既定にして `null` を返し、alt テキストへ落とすようにしています。
アイコンが欲しい用途では `ThumbnailFromFile(path, size, allowIcon: true)` を使います。

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

自作 4 つと SharpVectors 6 つ（約 1.8 MB）の計 10 個です。

```
MdLib.dll  MdWpf.dll  SvgWpf.dll  ImgWpf.dll
SharpVectors.Model.dll  SharpVectors.Rendering.Wpf.dll  SharpVectors.Converters.Wpf.dll
SharpVectors.Core.dll   SharpVectors.Css.dll            SharpVectors.Dom.dll
SharpVectors.Runtime.Wpf.dll
```

`ImgWpf.dll` は OS の機能だけを使うので、これに付随する DLL はありません。

`SharpVectors.Rendering.Gdi.dll` は NuGet が同梱しますが参照チェーンのどこからも使われていないため、
配布時は除外できます（削除した構成で動作確認済み）。

Avalonia 版は、Avalonia 本体（アプリ側が持つ）に加えて次が要ります。

```
MdLib.dll  MdAvalonia.dll  SvgAvalonia.dll  ImgAvalonia.dll
Avalonia.Svg.Skia.dll  Svg.Skia.dll  Svg.Model.dll  Svg.Custom.dll  ShimSkiaSharp.dll  ExCSS.dll
SkiaSharp.dll  SkiaSharp.HarfBuzz.dll  HarfBuzzSharp.dll（＋ OS ごとのネイティブ DLL）
```

**SkiaSharp は 3.116.1 になります**（Avalonia.Svg.Skia 11.3.0 がこの版を要求するため）。
Avalonia 11.3 は SkiaSharp 3 でも動きます（実測）。

---

## 既知の注意点

- `MdFlow.ToFlowDocument()` は WPF の Dispatcher が必要です。UI スレッドから呼んでください
- `SvgRender.From*()` / `ImgRender.*` の戻り値は `Freeze()` 済みなので、別スレッドから
  UI に渡せます
- コンソールアプリから使う場合、`Main` に `[STAThread]` が必要です。
  `ImgRender.ThumbnailFromFile()` は COM を使うため、これは必須です
- サムネイルの初回生成は動画で数百 ms かかることがあります。動画を多く貼った Markdown を
  同期的に描くと、その分 UI が止まります（キャッシュ機構は持たせていません。OS 側の
  サムネイルキャッシュに任せています）
- 動画は静止画のサムネイルです。**再生機能はありません**
- リンクのクリックは既定のブラウザで開きます（`Process.Start`）。Avalonia 版は `TopLevel.Launcher` を使い、
  `MdStyle.OpenLink` に処理を渡せばホスト側で開けます

---

## ライセンス

[MIT License](LICENSE) — Copyright (c) 2026 Keiji Mitsubuchi

SVG の解析に使っている [SharpVectors](https://github.com/ElinamLLC/SharpVectors) は
BSD-3-Clause（Copyright (c) 2010 - 2024 Elinam LLC）です。SharpVectors の DLL を同梱して
再配布する場合は、そちらの著作権表示とライセンス条文も併せて添付してください。

Avalonia 版が使う外部ライブラリと、そのライセンスは次のとおりです。DLL を同梱して再配布する場合は、
それぞれの著作権表示とライセンス条文も添付してください。

| ライブラリ | ライセンス |
|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | MIT |
| [Avalonia.Svg.Skia / Svg.Skia / Svg.Model / ShimSkiaSharp](https://github.com/wieslawsoltes/Svg.Skia) | MIT |
| **Svg.Custom**（[SVG.NET](https://github.com/svg-net/SVG) の派生） | **MS-PL** |
| [ExCSS](https://github.com/TylerBrinks/ExCSS) | MIT |
| [SkiaSharp / HarfBuzzSharp](https://github.com/mono/SkiaSharp) | MIT |

`MdLib` は外部依存がないため、Markdown 解析だけを使う場合は MIT のみで完結します。
