# 埋め込みフォント置き場

デザインの指定は UI = IBM Plex Sans JP、コードと数値 = JetBrains Mono。
どちらも SIL OFL 1.1 なので同梱・再配布できる。

この版ではフォントファイルを同梱しておらず、`Themes/Tokens.axaml` の
`FontFamilyUi` / `FontFamilyMono` が OS のフォントへフォールバックする指定になっている
（Linux では Noto Sans CJK JP / DejaVu Sans Mono、Windows では Yu Gothic UI / Consolas、
macOS では Hiragino Sans / SF Mono）。

同梱する場合は、このディレクトリに .ttf を置いたうえで `Tokens.axaml` を次のように書き換える。

```xml
<FontFamily x:Key="FontFamilyUi">avares://SQLForge/Assets/Fonts#IBM Plex Sans JP</FontFamily>
<FontFamily x:Key="FontFamilyMono">avares://SQLForge/Assets/Fonts#JetBrains Mono</FontFamily>
```

ディストリごとに日本語グリフのフォールバックが変わって行の高さが揺れるため、
配布物では埋め込む前提（design/avalonia.md 5 節）。
