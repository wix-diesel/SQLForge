# SQLForge — Avalonia / C# 実装への対応

`design/` のモックアップを Avalonia 12 + .NET で実装するときの対応表と注意点。
デザイントークンは `avalonia/Tokens.axaml`、主要コントロールテーマは `avalonia/Controls.axaml`。

## 1. 画面要素とコントロールの対応

| モックアップの要素 | Avalonia |
| --- | --- |
| カスタムタイトルバー | `Window` に `ExtendClientAreaToDecorationsHint="True"` + `ExtendClientAreaChromeHints="NoChrome"`。ドラッグは `BeginMoveDrag` |
| メニューバー | `Menu`。GNOME の大域メニューに載せるなら `NativeMenuBar` を併用 |
| 3 ペイン + スプリッター | `Grid` + `GridSplitter`（6px）。将来ペインを剥がせるようにするなら `Dock.Avalonia` |
| オブジェクトエクスプローラー | `TreeDataGrid`（`HierarchicalTreeDataGridSource`）。`TreeView` は列を持てないので行数バッジ付きの行を作れない |
| ツリーの遅延読み込み | ノード展開時に子を非同期取得。`HierarchicalExpanderColumn` の `hasChildrenSelector` でダミー展開を出す |
| SQL エディタ | `AvaloniaEdit`。ハイライトは `AvaloniaEdit.TextMate` + `TextMateSharp.Grammars` の SQL 文法 |
| ガターの変更マーカー・現在行 | AvaloniaEdit の `IBackgroundRenderer`（現在行）と `AbstractMargin` 派生（変更マーカー） |
| 補完ポップアップ | AvaloniaEdit の `CompletionWindow` + 自前の `ICompletionData`。バッジと型表示は `DataTemplate` で |
| エディタタブ | `TabControl`。閉じるボタンとダーティドットは `TabItem` の `HeaderTemplate` |
| 結果グリッド | `TreeDataGrid`（`FlatTreeDataGridSource`）。行仮想化あり |
| セル内データバー | `TemplateColumn` + `Grid` に `Border`（幅は最大値比のコンバータ） |
| NULL 表示・数値右寄せ | `TemplateColumn` の `CellTemplate`、または `TextColumn` + `IValueConverter` |
| 実行プランのツリー表 | `TreeDataGrid`（Hierarchical）。コスト割合は `TemplateColumn` のバー |
| ER 図キャンバス | 自作。`ZoomBorder`（Avalonia.Controls.PanAndZoom）の中に `Canvas`、ノードは `ItemsControl` + `Canvas.Left/Top`、エッジは `Path` + `StreamGeometry` |
| ER 図のミニマップ | 同じノード集合を縮小スケールで再描画する `ItemsControl`（`RenderTargetBitmap` でも可） |
| 接続ダイアログ・設定 | `Window` を `ShowDialog` |
| 設定の左ナビ | `ListBox` + `ContentControl`（`DataTemplates` で本文を差し替え） |
| トグルスイッチ | `ToggleSwitch` をリテーマ（32×18px、ノブ 14px） |
| PK / FK / UQ バッジ | `Border` + `TextBlock`。`Controls.axaml` の `KeyBadge` |
| アイコン | `PathIcon` + `StreamGeometry` リソース。モックアップの SVG `d` 属性をそのまま `StreamGeometry` に流用できる（16px ビューボックス、ストローク 1.3–1.5px） |
| トースト通知 | `WindowNotificationManager`（Avalonia.Controls.Notifications） |

## 2. 使うライブラリ

| 用途 | パッケージ |
| --- | --- |
| UI | `Avalonia`, `Avalonia.Themes.Fluent`, `Avalonia.Desktop` |
| ツリー・グリッド | `Avalonia.Controls.TreeDataGrid` |
| SQL エディタ | `Avalonia.AvaloniaEdit`, `AvaloniaEdit.TextMate`, `TextMateSharp.Grammars` |
| ER 図のパン・ズーム | `Avalonia.Controls.PanAndZoom` |
| ドッキング（任意） | `Dock.Avalonia` |
| MVVM | `CommunityToolkit.Mvvm`（テンプレート既定の ReactiveUI でも可） |
| DB ドライバ | `Npgsql` / `MySqlConnector` / `Microsoft.Data.Sqlite` / `ClickHouse.Client` / `Microsoft.Data.SqlClient` |
| キーリング | `Tmds.DBus` 経由で Secret Service（`org.freedesktop.secrets`） |

## 3. 自作が必要なもの（重い順）

1. **ER 図** — ノード配置、直交エッジのルーティング、crow's foot マーカー、自動整列（階層レイアウト）。既製品がないので全部自前。工数の山はここ。
2. **実行プランの解析と評価** — `EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)` をツリーに変換し、警告ルール（全件スキャン、推定と実測のずれ、ディスクへのソート溢れ）と改善案の DDL を出す。エンジンごとにプラン形式が違うので抽象化が要る。
3. **未適用の変更キュー** — 列定義グリッドの編集を差分として貯め、DDL 生成 + ロック影響と所要時間の見積りを出す。ロック影響の判定はエンジンとバージョンに依存する知識テーブルが必要。
4. **補完エンジン** — スキーマキャッシュ + カーソル位置の文脈判定（FROM 句のエイリアス解決まで）。素朴な前方一致だけなら軽いが、モックアップの精度を出すなら簡易パーサが要る。
5. **結果グリッドのストリーミング取得** — 取得上限とページングを `DbDataReader` の逐次読みに乗せる。1,000 行を超えたら追加取得。

## 4. テーマと密度の切り替え

テーマ:

```csharp
Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;   // / Light / Default
```

`Tokens.axaml` の `ThemeDictionaries` が自動で切り替わる。参照側は必ず `DynamicResource` で引くこと（`StaticResource` だと切り替わらない）。

密度は寸法の `x:Double` だけを別辞書に切り出し、実行時にマージ辞書を差し替える:

```csharp
var dict = (ResourceInclude)Application.Current!.Resources.MergedDictionaries[densityIndex];
dict.Source = new Uri("avares://SQLForge/Themes/Density.Comfortable.axaml");
```

**Fluent の上書きが要るキー** — `TextBox` / `ComboBox` / `CheckBox` は Fluent のテンプレートが状態別に自前のリソースキーを引くため、プロパティ設定だけでは hover / focus の色が戻る。テーマ辞書側で `TextControlBackground`, `TextControlBackgroundPointerOver`, `TextControlBackgroundFocused`, `TextControlBorderBrush*`, `TextControlForeground*` を上書きするのが確実。

## 5. フォントと日本語

- **フォントは埋め込む。** システムフォント任せだと、ディストリごとに日本語グリフのフォールバックが変わって行の高さが揺れる。`Assets/Fonts/` に IBM Plex Sans JP と JetBrains Mono を置き、`avares://` 参照にする。
- 等幅と日本語の混在（`128,940.50` と `北米` が同じ行に並ぶ）は、JetBrains Mono に CJK がないため必ずフォールバックが起きる。結果グリッドは**列単位でフォントを切り替える**（数値・識別子は等幅、テキスト列は UI フォント）方が揃う。モックアップもそうしてある。
- ライセンス: 両方 SIL OFL 1.1 なので同梱・再配布可。

## 6. Linux 固有の注意点

- **表示バックエンドは X11 が基本**。Wayland ネイティブバックエンドの現況は実装前に要確認で、通常は XWayland 経由で動く。モックアップのステータス表記もこれに合わせてある。
- **分数スケーリング**は X11 では素直に取れない。`AVALONIA_SCREEN_SCALE_FACTORS` を読む層を挟み、設定の「高 DPI で自動的にスケール」でそれを制御する。
- **配布**: `dotnet publish -r linux-x64 --self-contained` の上で Flatpak を主軸に（`org.freedesktop.Sdk`）、副として AppImage と `.deb`。Flatpak なら Secret Service ポータル経由でキーリングが使える。
- **クリップボード**は `TopLevel.GetTopLevel(control)?.Clipboard`。グリッドからの CSV / TSV コピーはここ。
- **パスワードはファイルに書かない。** 接続情報は TOML、資格情報は Secret Service。読み取れなければ都度入力にフォールバックする。

## 7. 性能で気をつけるところ

- `TreeDataGrid` は行仮想化は効くが、**列方向の仮想化は弱い**。結果グリッドの列が数十を超えるケース（`SELECT *` の広いテーブル）は実機で要検証。だめなら列の可視範囲を自前で絞る。
- 26px 行 × 1,000 行の初回描画と、スクロール中のセルテンプレート再生成がコスト。データバーや NULL 表示を `TemplateColumn` で作ると重くなりやすいので、まずは `TextColumn` + フォーマッタで組んで、必要な列だけテンプレート化する。
- クエリ実行は必ず `CancellationToken` 付きの非同期で。ツールバーの「停止」がそのまま `NpgsqlCommand.Cancel()` / トークンのキャンセルに対応する。

## 8. フェーズ分け（確定）

ER 図と実行プランビジュアライザは、それぞれ単体で他の全画面に匹敵する工数がある。
フェーズ 1 の範囲は確定済み。

**フェーズ 1 — 動く SQL クライアント**

| 画面 | モックアップ | 主な構成要素 |
| --- | --- | --- |
| 接続ダイアログ | `Connect.dc.html` | 保存済み接続の一覧、ドライバー選択、環境タグ、読み取り専用モード、Secret Service 連携 |
| メインウィンドウ | `Main.dc.html` | オブジェクトエクスプローラー（TreeDataGrid・遅延読み込み）、SQL エディタ（AvaloniaEdit + TextMate + 補完）、結果グリッド（TreeDataGrid・ストリーミング取得）、インスペクタ |
| 設定 | `Settings.dc.html` | テーマ / アクセント / 密度 / フォント。`Tokens.axaml` の切り替えがそのまま裏側 |

フェーズ 1 に含める安全機構: 環境タグの常時表示と、本番接続の既定読み取り専用モード。
どちらも接続情報に属するので後付けが効かない。

**フェーズ 2** — テーブル設計と未適用の変更キュー（`Schema.dc.html`）。SSMS 相当を名乗れる分岐点。

**フェーズ 3** — 実行プランビジュアライザ（`Plan.dc.html`）、次いで ER 図（`ERD.dc.html`）。

3 ペインの骨格・密度・トークンは全画面で共通にしてあるので、フェーズ 2 以降を足しても
フェーズ 1 のレイアウトは変わらない。
