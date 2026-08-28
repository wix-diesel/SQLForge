# 画面まわり

| ファイル | 中身 |
| --- | --- |
| `Themes/Tokens.axaml` | デザイントークン（`design/avalonia/Tokens.axaml` 由来）。色はテーマ別、寸法は共通 |
| `Themes/Controls.axaml` | コントロールテーマ。ControlTheme と Style を 1 ファイルに収めるため `Styles` として書く |
| `Themes/Icons.axaml` | モックアップの SVG パスをそのまま持ってきた 16px のアイコン |
| `Views/ConnectWindow.axaml` | 起動時のウィンドウ。左ペイン・タブ・フッターを組み合わせる |
| `Views/MainWindow.axaml` | 接続後のウィンドウ。エクスプローラー・作業領域・ステータスバー |
| `Views/ObjectExplorerPane.axaml` | オブジェクトエクスプローラー。ノードの種類ごとに `TreeDataTemplate` を持つ |
| `Views/ObjectFilterWindow.axaml` | SSMS の「フィルターの設定」。条件にできるプロパティを 1 行ずつ並べ、演算子と値を入れてもらう。行は増減しない |
| `ViewModels/Explorer/FolderNode.cs` | 一覧を持つ見出しノードの共通部分。件数の表示と絞り込み（条件・「(フィルター適用)」・読み直し）はここ |
| `Views/QueryWorkspacePane.axaml` | クエリのタブ帯・エディタ（`AvaloniaEdit` の `TextEditor`）・結果ペイン。列幅はビューモデルが配り、見出しとセルが同じ値を引く。コードビハインドがやるのは色分けの差し込み・補完ポップアップの出し入れ・タブごとのキャレット位置の出し入れ・タブの中クリックだけ |
| `ViewModels/Workspace/QueryEditorViewModel.cs` | タブ帯。開く・切り替える・閉じるだけを持つ |
| `ViewModels/Workspace/QueryDocumentViewModel.cs` | タブ 1 枚（SSMS のクエリ ウィンドウ 1 つ）。文面・実行先・実行・結果はここ |
| `Presentation/SqlColorizer.cs` | 字句ごとに色を当てる `DocumentColorizingTransformer`。色は `Syntax*Brush` から引き、テーマが変わったら取り直す |
| `Presentation/SqlCompletionData.cs` | 補完ポップアップ 1 行。ユースケースが作った候補を AvaloniaEdit へ渡す包み |
| `Views/TableEditorPane.axaml` | 先頭 100 行の編集グリッド。セルは「表示」と「入力欄」の 2 枚を重ね、開いた側だけを出す。いちばん下は追加用の `*` 行 |

色はすべてトークン経由の `DynamicResource` で引いているので、ライトテーマは
`Application.Current.RequestedThemeVariant` を変えるだけで切り替わる（設定画面はまだない）。

ツリーの字下げは Fluent の既定に頼らず、`TreeViewItem.Level` を `TreeIndentConverter` で
余白に写している（モックアップの 13px 刻みに合わせるため）。

見出しの行に出す文字は `Title` ではなく `DisplayTitle` を引く。絞り込みが掛かっている間だけ
「(フィルター適用)」が付くのを、テンプレート側で組み立てずに済ませるため。

フォントは同梱していない。OS のフォントへ順にフォールバックする指定にしてあるので、
配布時は [src/SQLForge.Ui/Assets/Fonts/README.md](../src/SQLForge.Ui/Assets/Fonts/README.md) のとおり
IBM Plex Sans JP と JetBrains Mono を埋め込む。
