# SQLForge

Linux で SQL Server を操作するためのアプリ。

UI デザインは [`design/`](design/README.md) にある。実装は Avalonia 11 + .NET 10。

## 現在の状態

**フェーズ 1 のうち、接続ダイアログ・オブジェクトエクスプローラー・クエリ実行**が動く。

アプリを起動すると接続ダイアログが開く。保存済み接続の一覧・検索、接続情報の入力と検証、
環境タグ、読み取り専用モードのトグル、接続 URL の組み立てまでが動く。

**SQL Server へは実際に接続する。**「接続をテスト」は本当にサーバーへ繋いで素性を読み、
「接続」が通るとメインウィンドウが開いて、データベース → スキーマ → テーブルをツリーで辿れる。

**クエリも実際に実行する。**ツリーでテーブルかデータベースを右クリックして
「クエリを実行」（データベースなら「新しいクエリ」）を選ぶと、右の作業領域に**空の**エディタが開く。
決まるのは実行先のデータベースだけ。書いて「実行」（F5 / Ctrl+Enter）を押すと、
下の結果ペインにグリッドで結果が出る。

| 部分 | 現状 |
| --- | --- |
| SQL Server への接続 | 動く（`Microsoft.Data.SqlClient`）。パスワード認証と OS 統合認証、TLS 要求レベルの指定 |
| データベース一覧・スキーマ一覧・テーブル一覧 | 動く。展開したところだけを読む遅延読み込み。テーブルは概算行数付き |
| PostgreSQL / MySQL / ClickHouse / SQLite | ドライバー未実装。選んで接続すると「未対応」と出る（成功に見せかけない） |
| 保存済み接続の永続化 | プロセス内のみ（`InMemoryConnectionProfileRepository`）。TOML への保存は未実装 |
| 資格情報 | プロセス内のみ（`InMemorySecretStore`）。Secret Service / 資格情報マネージャー / キーチェーンは未接続 |
| クエリの実行 | 動く。読み書きとも。複数の結果セット、影響行数、実行時間、取得上限 1,000 行での打ち切り、実行の取り消し |
| テーブルの中身をのぞく既定の文面 | 未実装（別の入口として用意する予定）。右クリックで開くのは空のエディタ |
| 結果グリッド | 動く。行の仮想化つき。NULL は薄字、数値の列は右寄せ、列幅は中身から自動で決める |
| クエリエディタ | 素の `TextBox`。構文の色分け・補完・整形・複数タブは未実装（`AvaloniaEdit` はまだ入れていない） |
| ツリーの絞り込み | 未実装（入力欄は無効で置いてある） |
| SSH トンネル / TLS の証明書指定 / 詳細設定タブ | タブは出るが中身は未実装（TLS の要求レベルだけ「一般」タブにある） |
| 設定画面 | 未着手（フェーズ 1 の残り） |

デザインからの意図的な差分が 3 点ある。

- モックアップでは接続一覧の選択行に緑の接続中ドットが付くが、一覧の時点では接続していないので、
  代わりに読み取り専用で開く接続へ盾のアイコンを出している。
- `design/avalonia.md` はオブジェクトエクスプローラーに `TreeDataGrid` を挙げているが、
  今出している行（名前・件数・行数）は 1 行の中に収まるので `TreeView` で足りている。
  列として揃えたくなった時点で差し替える。
- 結果グリッドも `TreeDataGrid` ではなく、`ListBox` に列幅を自前で配って組んでいる。
  行の仮想化は `ListBox` で足りていて、パッケージを 1 つ増やすほどの差がまだ無いため。
  列の並べ替えやリサイズを入れる時点で差し替える。`design/avalonia.md` の「列単位で
  フォントを切り替える」も入れていない（全部等幅で出し、日本語は OS のフォールバックに任せる）。

## 動かす

```sh
dotnet run --project src/SQLForge.Ui      # 起動すると接続ダイアログが開く
dotnet test                                # ヘッドレス描画テストを含む
```

.NET 10 SDK が要る（`global.json` で 10.0 系に固定している）。
Linux では X11（Wayland では XWayland 経由）で動く。

Avalonia は 11.3 系を使う。11.3 のパッケージが配っているのは net8.0 向けアセットだが、
net10.0 からそのまま参照できる。

### SQL Server へ繋ぐ

ダイアログの「新しい接続」は SQL Server が既定になっている。ホスト・ポート（既定 1433）・
データベース・ユーザー・パスワードを入れて「接続をテスト」か「接続」を押す。

TLS の要求レベルは接続文字列へこう写す。

| 「一般」タブの TLS | SqlClient | 意味 |
| --- | --- | --- |
| なし / 推奨 | `Encrypt=Optional`, `TrustServerCertificate=true` | クライアントからは必須にしない（サーバーが要求すれば張られる） |
| 必須 | `Encrypt=Mandatory`, `TrustServerCertificate=true` | 暗号化は必須。証明書は検証しない |
| 完全検証 | `Encrypt=Mandatory`, `TrustServerCertificate=false` | 暗号化必須かつ証明書を検証する |

接続テストの結果に出る「TLS 有効 / なし」は、要求した設定ではなくサーバーに問い合わせた実際の状態
（`sys.dm_exec_connections`）。この DMV は `VIEW SERVER STATE` 権限が要るので、読めない接続では
「TLS 不明 (要求 …)」と出して推測しない。

### クエリを実行する

ツリーのテーブルかデータベースを右クリック → メニューの項目で作業領域が開く。
開くのは**空のエディタ**で、決まるのは実行先のデータベースだけ。
テーブルの中身をのぞく既定の文面（SSMS の「上位 N 行の選択」にあたるもの）は、
別の入口として用意する予定で、まだ無い。

実行するとエンジンへ文面をそのまま送る。分割も書き換えもしないので、複数の文をまとめて投げられる。
結果セットが複数返れば「結果 1」「結果 2」…とタブが並び、行を返さない文の件数と実行時間は「メッセージ」に出る。

| ふるまい | 中身 |
| --- | --- |
| 実行先のデータベース | 実行の直前に `USE [db]` で合わせる。エディタの文面は修飾なしで書かれるのが普通なため |
| 書き込み | 止めない。`INSERT` も DDL もそのまま送る |
| 取得上限 | 結果セットごとに 1,000 行で打ち切る。打ち切ったことは結果ペインとメッセージに出す（黙って切らない） |
| 取り消し | ツールバーの「停止」。`CancellationToken` がそのままドライバーへ渡る |
| 値の見せ方 | 不変カルチャで文字列へ写す。NULL は薄字の `NULL`（`'NULL'` という値と見分けられるように区別して持つ） |
| 同時実行 | カタログの照会と同じ門を通す。ツリーを展開しながら実行しても接続を取り合わない |

**接続の「読み取り専用」は印であって、書き込みを止める仕掛けではない**（`AccessMode.ReadOnly`）。
一覧とステータスバーに盾のアイコンを出すだけで、文面は素通しする。文面から書き込みかどうかを
見分ける仕掛けは動的 SQL のような形をどうせ通してしまい、あると「止まるはず」という誤解を招くので、
止めるのはサーバー側の権限の仕事にしてある。書かせたくない接続には読み取り専用のロールを与えること。

## 構成

オニオンアーキテクチャ。依存は外から内へ一方向で、内側は外側を知らない。

```
SQLForge.Domain          接続情報のモデルと規則（環境タグ、ドライバー、接続 URL の組み立て）と、
                         カタログのモデル（データベース / スキーマ / テーブル / サーバーの素性）と、
                         クエリ結果のモデル（列 / 行 / 結果セット）
      ↑
SQLForge.Application     ユースケースとポート（IDatabaseConnector / IDatabaseSession /
                         IConnectionProfileRepository / IConnectionProbe / ISecretStore /
                         IPlatformProfile）。入力は ConnectionDraft で受ける
      ↑
SQLForge.Infrastructure  ポートの実装のうち、DBMS に依らないもの。
      |                  接続の台帳・接続テスト・ADO.NET 共通の足回り（AdoDatabaseSession）と、
      |                  保存済み接続・キーリング・OS 判定
      ↑
SQLForge.Infrastructure.SqlServer
                         SQL Server ドライバー。Microsoft.Data.SqlClient を抱えるのはここだけ
      ↑
SQLForge.Ui              Avalonia のビューとビューモデル。合成ルートは Composition/AppServices
```

**ドライバーは DBMS ごとに別プロジェクト**にしてある。1 つの Infrastructure に全部入れると、
DBMS を 1 つ足すたびに全利用者がその依存（SqlClient・Npgsql・…）を引き込むことになるため。
この境目は口約束ではなく `LayerDependencyTests` が組み上がったアセンブリの参照から見張っている。

エンティティ（`ConnectionProfile`）は常に妥当である前提なので、編集中の値は
`ConnectionDraft` で持ち、検証を通ってからエンティティへ変換する。

### DBMS を増やすとき

PostgreSQL などを足すときに触るのは **新しいドライバープロジェクトと合成ルートの 1 行だけ**で、
Domain・Application・UI と既存のドライバーは変わらない。

1. `src/SQLForge.Infrastructure.PostgreSql`（例）を作り、`SQLForge.Infrastructure` を参照して
   ドライバーのパッケージ（`Npgsql` など）を入れる。SQL Server 側がそのまま雛形になる
2. `IDatabaseConnector` の実装を書く（接続を開いてサーバーの素性を読む）
3. `AdoDatabaseSession` を継承してカタログの読み方を 3 つ埋め
   （`ReadDatabasesAsync` / `ReadSchemasAsync` / `ReadTablesAsync`）、
   実行先の切り替え方（`SwitchDatabaseAsync`）を書く。
   クエリの結果を読むところは `AdoDatabaseSession` が持っているので触らなくてよい
4. `SQLForge.Ui` から新しいプロジェクトを参照し、`AppServices.AddInfrastructure` に
   `services.AddSingleton<IDatabaseConnector, XxxConnector>()` を足す
5. `LayerDependencyTests.DriverAssemblies` に新しいパッケージ名を足す
   （共通の Infrastructure へ混ざり込んだら落ちるようにするため）

`DatabaseConnectorRegistry` は登録された実装を勝手に拾うので、台帳も接続テストも接続も、
未対応の文言も、追加したドライバーへそのまま追随する。

エンジン差はセッションの実装に閉じ込める。たとえば SQL Server は 3 部名（`[db].sys.tables`）で
他のデータベースのカタログを読めるが、PostgreSQL はデータベースをまたげないので接続を張り直す。
どちらでも `IDatabaseSession` の形は変わらない。

`AdoDatabaseSession` は接続の寿命と照会の直列化だけを引き受ける。ツリーは複数のノードを
同時に展開できる一方、`DbConnection` 1 本で照会を同時に走らせることはできないためで、
これはどのドライバーでも同じ事情になる。

### 画面まわり

| ファイル | 中身 |
| --- | --- |
| `Themes/Tokens.axaml` | デザイントークン（`design/avalonia/Tokens.axaml` 由来）。色はテーマ別、寸法は共通 |
| `Themes/Controls.axaml` | コントロールテーマ。ControlTheme と Style を 1 ファイルに収めるため `Styles` として書く |
| `Themes/Icons.axaml` | モックアップの SVG パスをそのまま持ってきた 16px のアイコン |
| `Views/ConnectWindow.axaml` | 起動時のウィンドウ。左ペイン・タブ・フッターを組み合わせる |
| `Views/MainWindow.axaml` | 接続後のウィンドウ。エクスプローラー・作業領域・ステータスバー |
| `Views/ObjectExplorerPane.axaml` | オブジェクトエクスプローラー。ノードの種類ごとに `TreeDataTemplate` を持つ |
| `Views/QueryWorkspacePane.axaml` | クエリエディタと結果ペイン。列幅はビューモデルが配り、見出しとセルが同じ値を引く |

色はすべてトークン経由の `DynamicResource` で引いているので、ライトテーマは
`Application.Current.RequestedThemeVariant` を変えるだけで切り替わる（設定画面はまだない）。

ツリーの字下げは Fluent の既定に頼らず、`TreeViewItem.Level` を `TreeIndentConverter` で
余白に写している（モックアップの 13px 刻みに合わせるため）。

フォントは同梱していない。OS のフォントへ順にフォールバックする指定にしてあるので、
配布時は `src/SQLForge.Ui/Assets/Fonts/README.md` のとおり IBM Plex Sans JP と
JetBrains Mono を埋め込む。

## OS の想定

初期版の対象は Linux だが、Windows と macOS でもそのまま起動できるようにしてある。
OS 依存は `IPlatformProfile`（Infrastructure の `PlatformProfile`）に閉じ込めてあり、
現状の分岐はウィンドウ装飾だけ — Linux / Windows はモックアップどおりの自前タイトルバー、
macOS は信号機ボタンがあるので OS 標準の装飾を使う。

OS 統合認証（Windows 認証）は Linux では Kerberos の設定が要る。整っていない環境では
接続時に SqlClient がその理由を返すので、ダイアログのフッターにそのまま出る。
