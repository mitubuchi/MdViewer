namespace MdLib
{
    /// <summary>対応している記法のサンプル。テストアプリと仕様確認の両方に使う</summary>
    public static class MdSamples
    {
        public const string Markdown = @"# MdWpf テスト

これは **太字** と *斜体* と ***両方***、`インラインコード` の例です。
行末に半角スペース2個を置くと
明示的な改行になります。

## リスト

- 項目A
- 項目B
  - 子B1
  - 子B2
    - 孫B2a
- 項目C

1. 一番
2. 二番
   1. 二の一
3. 三番

## コードブロック

```csharp
public static Vobject operator +(Vobject a, Vobject b)
{
    if (a.IsString || b.IsString)
        return new Vobject(a.ToString() + b.ToString(), typeof(string));
    return new Vobject(a.AsNumber() + b.AsNumber(), typeof(Number));
}
```

## 引用

> 引用の1行目
> - 引用内のリスト
>
> ### 引用内の見出し

## 表

| 型 | 名前 | 比較に使う値 |
|------|:------:|-------------:|
| Number | int / double | AsDouble() |
| Fraction | 分数 | ToDouble() |
| Complex | 複素数 | Abs() |
| Vector | ベクトル | Abs() |

---

## リンクと画像

[Anthropic](https://www.anthropic.com) へのリンク。

存在しない画像は alt で代替されます: ![no image](notfound.png)

エスケープ: \*star\* \_under\_ \`tick\` \# hash
";
    }
}
