# 画面まわり

| ファイル | 中身 |
| --- | --- |
| `Themes/Tokens.axaml` | デザイントークン（`design/avalonia/Tokens.axaml` 由来）。色はテーマ別、寸法は共通 |
| `Themes/Controls.axaml` | コントロールテーマ。ControlTheme と Style を 1 ファイルに収めるため `Styles` として書く |
| `Themes/Icons.axaml` | モックアップの SVG パスをそのまま持ってきた 16px のアイコン |
| `Views/ConnectWindow.axaml` | 起動時のウィンドウ。左ペイン・タブ・フッターを組み合わせる |
| `Views/MainWindow.axaml` | 接続後のウィンドウ。エクスプローラー・作業領域・ステータスバー |
| `Views/ObjectExplorerPane.axaml` | オブジェクトエクスプローラー。ノードの種類ごとに `TreeDataTemplate` を持つ |
| `Views/QueryWorkspacePane.axaml` | クエリエディタと結果ペイン。列幅はビューモデルが配り、見出しとセルが同じ値を引く |
| `Views/TableEditorPane.axaml` | 先頭 100 行の編集グリッド。セルは「表示」と「入力欄」の 2 枚を重ね、開いた側だけを出す。いちばん下は追加用の `*` 行 |

色はすべてトークン経由の `DynamicResource` で引いているので、ライトテーマは
`Application.Current.RequestedThemeVariant` を変えるだけで切り替わる（設定画面はまだない）。

ツリーの字下げは Fluent の既定に頼らず、`TreeViewItem.Level` を `TreeIndentConverter` で
余白に写している（モックアップの 13px 刻みに合わせるため）。

フォントは同梱していない。OS のフォントへ順にフォールバックする指定にしてあるので、
配布時は [src/SQLForge.Ui/Assets/Fonts/README.md](../src/SQLForge.Ui/Assets/Fonts/README.md) のとおり
IBM Plex Sans JP と JetBrains Mono を埋め込む。
