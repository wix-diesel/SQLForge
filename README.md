# SQLForge

Linux で SQL Server を操作するためのアプリ。

UI デザインは [`design/`](design/README.md) にある。実装は Avalonia 11 + .NET 10。

## 現在の状態

**フェーズ 1 のうち、接続ダイアログとオブジェクトエクスプローラー**が動く。

アプリを起動すると接続ダイアログが開く。保存済み接続の一覧・検索、接続情報の入力と検証、
環境タグ、読み取り専用モードのトグル、接続 URL の組み立てまでが動く。

**SQL Server へは実際に接続する。**「接続をテスト」は本当にサーバーへ繋いで素性を読み、
「接続」が通るとメインウィンドウが開いて、データベース → スキーマ → テーブルをツリーで辿れる。

| 部分 | 現状 |
| --- | --- |
| SQL Server への接続 | 動く（`Microsoft.Data.SqlClient`）。パスワード認証と OS 統合認証、TLS 要求レベルの指定 |
| データベース一覧・スキーマ一覧・テーブル一覧 | 動く。展開したところだけを読む遅延読み込み。テーブルは概算行数付き |
| PostgreSQL / MySQL / ClickHouse / SQLite | ドライバー未実装。選んで接続すると「未対応」と出る（成功に見せかけない） |
| 保存済み接続の永続化 | プロセス内のみ（`InMemoryConnectionProfileRepository`）。TOML への保存は未実装 |
| 資格情報 | プロセス内のみ（`InMemorySecretStore`）。Secret Service / 資格情報マネージャー / キーチェーンは未接続 |
| クエリエディタ・結果グリッド | 未着手（フェーズ 1 の残り）。メインウィンドウの右側は今のところ選択中のオブジェクトを出すだけ |
| ツリーの絞り込み | 未実装（入力欄は無効で置いてある） |
| SSH トンネル / TLS の証明書指定 / 詳細設定タブ | タブは出るが中身は未実装（TLS の要求レベルだけ「一般」タブにある） |
| 設定画面 | 未着手（フェーズ 1 の残り） |

デザインからの意図的な差分が 2 点ある。

- モックアップでは接続一覧の選択行に緑の接続中ドットが付くが、一覧の時点では接続していないので、
  代わりに読み取り専用で開く接続へ盾のアイコンを出している。
- `design/avalonia.md` はオブジェクトエクスプローラーに `TreeDataGrid` を挙げているが、
  今出している行（名前・件数・行数）は 1 行の中に収まるので `TreeView` で足りている。
  列として揃えたくなった時点で差し替える。

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

## 構成

オニオンアーキテクチャ。依存は外から内へ一方向で、内側は外側を知らない。

```
SQLForge.Domain          接続情報のモデルと規則（環境タグ、ドライバー、接続 URL の組み立て）と
                         カタログのモデル（データベース / スキーマ / テーブル / サーバーの素性）
      ↑
SQLForge.Application     ユースケースとポート（IDatabaseConnector / IDatabaseSession /
                         IConnectionProfileRepository / IConnectionProbe / ISecretStore /
                         IPlatformProfile）。入力は ConnectionDraft で受ける
      ↑
SQLForge.Infrastructure  ポートの実装。SQL Server ドライバーはここだけにある
      ↑
SQLForge.Ui              Avalonia のビューとビューモデル。合成ルートは Composition/AppServices
```

エンティティ（`ConnectionProfile`）は常に妥当である前提なので、編集中の値は
`ConnectionDraft` で持ち、検証を通ってからエンティティへ変換する。

### DBMS を増やすとき

PostgreSQL などを足すときに触るのは **Infrastructure と合成ルートの 1 行だけ**で、
Domain・Application・UI は変わらない。

1. `IDatabaseConnector` の実装を書く（接続を開いてサーバーの素性を読む）
2. `AdoDatabaseSession` を継承してカタログの読み方を 3 つ埋める
   （`ReadDatabasesAsync` / `ReadSchemasAsync` / `ReadTablesAsync`）
3. `AppServices.AddInfrastructure` に `services.AddSingleton<IDatabaseConnector, XxxConnector>()` を足す

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
