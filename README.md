# SQLForge

Linux で SQL Server を操作するためのアプリ。

UI デザインは [`design/`](design/README.md) にある。実装は Avalonia 12 + .NET 10。

## 現在の状態

**フェーズ 1 のうち、接続ダイアログ・オブジェクトエクスプローラー・クエリ実行**が動く。

アプリを起動すると接続ダイアログが開く。保存済み接続の一覧・検索、接続情報の入力と検証、
環境タグ、読み取り専用モードのトグル、接続 URL の組み立てまでが動く。

**接続情報は保存して使い回せる。**「保存」を押すと OS の設定ディレクトリの
`connections.toml` に残り、次の起動でも左ペインに並ぶ。**左ペインの行を押すと、
その接続でそのまま繋ぎに行く**（パスワードは OS のキーリングから取り出す）。
パスワードを預けていない接続では、繋ぐ前に入力を促す。

**SQL Server へは実際に接続する。**「接続をテスト」は本当にサーバーへ繋いで素性を読み、
「接続」が通るとメインウィンドウが開いて、データベース → スキーマ → テーブルをツリーで辿れる。

**データベース ユーザーは実際に読み書きする。**SSMS と同じく、データベースの下の
「セキュリティ → ユーザー」に一覧が並ぶ。「ユーザー」を右クリックして「新しいユーザー…」、
ユーザーを右クリックして「プロパティ」「削除」を選ぶと、SSMS の「データベース ユーザー」
ダイアログにあたる画面が開く。種類・ログイン名・既定のスキーマ・ロールのメンバーシップを
編集して OK を押すと、`CREATE USER` / `ALTER USER` / `DROP USER` が実行される。

**サーバー単位のログインも読み書きする。**SSMS と同じく、サーバーの下の
「セキュリティ → ログイン」に一覧が並ぶ。「ログイン」を右クリックして「新しいログイン…」、
ログインを右クリックして「プロパティ」「削除」を選ぶと、SSMS の「ログイン」ダイアログにあたる
画面が開く。認証方式・パスワードと規則（ポリシー・期限・次回変更）・既定のデータベース・
サーバー ロールのメンバーシップ・有効と無効を編集して OK を押すと、
`CREATE LOGIN` / `ALTER LOGIN` / `DROP LOGIN` が実行される。

**クエリも実際に実行する。**ツリーでテーブルかデータベースを右クリックして
「クエリを実行」（データベースなら「新しいクエリ」）を選ぶと、右の作業領域に**空の**エディタが開く。
決まるのは実行先のデータベースだけ。書いて「実行」（F5 / Ctrl+Enter）を押すと、
下の結果ペインにグリッドで結果が出る。

| 部分 | 現状 |
| --- | --- |
| SQL Server への接続 | 動く（`Microsoft.Data.SqlClient`）。パスワード認証と OS 統合認証（Windows 認証 / Kerberos）、TLS 要求レベルの指定 |
| データベース一覧・スキーマ一覧・テーブル一覧 | 動く。展開したところだけを読む遅延読み込み。テーブルは概算行数付き |
| データベース ユーザーの一覧・追加・編集・削除 | 動く。SQL ユーザー（ログインあり／なし）と Windows ユーザー／グループ。既定のスキーマとロールのメンバーシップも編集できる |
| サーバー ログインの一覧・追加・編集・削除 | 動く。SQL Server 認証と Windows 認証（ユーザー／グループ）。パスワードの規則、既定のデータベース、サーバー ロールのメンバーシップ、有効と無効も編集できる |
| ロールの追加編集・スキーマの所有権・ユーザー マッピング・セキュリティ保護可能なリソース | 未実装。ユーザーのダイアログではログイン名を手で入力する |
| PostgreSQL / MySQL / ClickHouse / SQLite | ドライバー未実装。選んで接続すると「未対応」と出る（成功に見せかけない） |
| 保存済み接続の永続化 | 動く。OS の設定ディレクトリの `connections.toml`（Linux は `~/.config/sqlforge/`、Windows は `%APPDATA%\sqlforge\`）。パスワードは書かない |
| 資格情報 | 動く。Windows は資格情報マネージャー、macOS はキーチェーン、Linux は Secret Service（`secret-tool`）。どれも使えない環境では都度入力になる |
| 保存済み接続の削除・書き出し・取り込み | 未実装。消すときは `connections.toml` を直接編集する |
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

Avalonia は 12.1 系を使う。net10.0 向けアセットが同梱されている。

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

### OS 統合認証（Windows 認証）で繋ぐ

「一般」タブの認証方式で **OS 統合認証** を選ぶと、SQL Server へは
`Integrated Security=true` で繋ぐ。名乗る相手を決めるのは OS なので、
利用者名もパスワードも接続文字列には載らない。

そのぶんダイアログの見え方も変わる。

| 認証方式 | 「一般」タブに出るもの |
| --- | --- |
| パスワード | ユーザーの入力欄と、パスワードの入力欄（キーリングへ預けるトグル付き） |
| OS 統合認証 | 打てる欄の代わりに、**実際に名乗る OS アカウント名**を読み取り専用で表示 |

打てる欄を残さないのは、打った値が使われないため。ユーザー欄に何か入っていても、
統合認証を選んだ時点でその値は保存内容にも接続 URL にも残らない
（`ConnectionCredentials` が落とす）。同じ理由でパスワードもキーリングへ預けない。

出る OS アカウント名は OS ごとに違う。

| OS | 名乗るアカウント | Kerberos の下ごしらえ |
| --- | --- | --- |
| Windows | `WindowsIdentity.GetCurrent().Name`（`DOMAIN\user` の形） | 要らない。OS の資格情報がそのまま渡る |
| Linux / macOS | ログイン名（`Environment.UserName`） | 要る。`kinit` で資格情報を取っておく |

Kerberos の用意が要る OS では、統合認証を選ぶとその旨をダイアログに出す。
それでも整っていなければ接続時に SqlClient が理由を返すので、フッターにそのまま出る。

確認用の接続 URL にも認証方式が出る。統合認証には権限部に書ける利用者名が無いので、
代わりにクエリへ `integrated_security=true` を足す。

```
パスワード認証: sqlserver://analyst_ro@db.internal:1433/sales_db?sslmode=require&application_name=sqlforge
OS 統合認証:    sqlserver://db.internal:1433/sales_db?sslmode=require&integrated_security=true&application_name=sqlforge
```

### 接続情報を保存する

「保存」を押すと、接続情報は OS の設定ディレクトリの `connections.toml` に残る。
次の起動でも左ペインに並び、**行を押すとその接続で繋ぎに行く**。

| OS | 接続情報 (TOML) | パスワードの預け先 |
| --- | --- | --- |
| Linux | `~/.config/sqlforge/connections.toml`（本人だけが読める 0600） | Secret Service（`secret-tool` ごし。GNOME キーリング / KWallet など） |
| Windows | `%APPDATA%\sqlforge\connections.toml` | 資格情報マネージャーの汎用資格情報（`sqlforge:<接続の Id>`） |
| macOS | OS の設定ディレクトリの `sqlforge/connections.toml` | ログイン キーチェーンの汎用パスワード（項目名 `sqlforge`） |

**パスワードは TOML には書かない。**ファイルに残るのは「どこへ誰として繋ぐか」までで、
パスワードは `ISecretStore` の OS ごとの実装が OS のキーリングへ預ける。SQLForge 自身は
暗号鍵を持たない — 暗号化と、取り出してよい相手かの判断は OS の担当にしてある。

キーリングを使えない環境（`secret-tool` が入っていない、セッション バスが無い、など）では
「キーリングを利用できません」と出して保存トグルを無効にする。パスワードは都度入力になるが、
接続情報そのものの保存は使える。

左ペインの行を押したときの動きは次のとおり。

| 状態 | 押したとき |
| --- | --- |
| パスワードを預けてある / OS 統合認証 | そのまま接続して、メインウィンドウが開く |
| パスワードが要るのに預けていない | 入力欄へ写して「パスワードが必要です」と出す（サーバーへは行かない） |
| 入力欄にパスワードを打ってある | 打った値で接続する（預けてあるものより優先する） |

キーボードでの上下移動や、起動直後に先頭が選ばれることでは繋ぎに行かない。
押した操作だけを合図にしてあるのは、開くつもりのない接続—とくに本番—へ
うっかり繋いでしまわないようにするため。

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
                         セキュリティのモデル（データベース ユーザーとサーバー ログインの
                         種類・名前・定義）と、
                         クエリ結果のモデル（列 / 行 / 結果セット）
      ↑
SQLForge.Application     ユースケースとポート（IDatabaseConnector / IDatabaseSession /
                         IConnectionProfileRepository / IConnectionProbe / ISecretStore /
                         IPlatformProfile）。入力は ConnectionDraft で受ける
      ↑
SQLForge.Infrastructure  ポートの実装のうち、DBMS にも OS にも依らないもの。
      |                  接続の台帳・接続テスト・ADO.NET 共通の足回り（AdoDatabaseSession）と、
      |                  保存済み接続・キーリングと、OS ごとの体裁の共通部分
      |                  （PlatformProfileBase・HostPlatform・PlatformProfileRegistry）
      ↑
SQLForge.Infrastructure.SqlServer
      |                  SQL Server ドライバー。Microsoft.Data.SqlClient を抱えるのはここだけ
      |
SQLForge.Infrastructure.Linux / .Windows / .MacOs
                         OS ごとの体裁。OS 依存を抱えるのはここだけ
      ↑
SQLForge.Ui              Avalonia のビューとビューモデル。合成ルートは Composition/AppServices
```

**ドライバーは DBMS ごとに、体裁は OS ごとに別プロジェクト**にしてある。1 つの Infrastructure に
全部入れると、DBMS や OS を 1 つ足すたびに全利用者がその依存（SqlClient・Npgsql・…）や
分岐を引き込むことになるため。どちらの境目も口約束ではなく `LayerDependencyTests` が
組み上がったアセンブリの参照から見張っている。

エンティティ（`ConnectionProfile`・`DatabaseUserDefinition`）は常に妥当である前提なので、
編集中の値は Draft（`ConnectionDraft`・`DatabaseUserDraft`）で持ち、
検証を通ってからエンティティへ変換する。

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
OS 依存は `IPlatformProfile` に閉じ込めてあり、その実装は **OS ごとに別プロジェクト**
（`SQLForge.Infrastructure.Linux` / `.Windows` / `.MacOs`）に置いてある。
現状の分岐はウィンドウ装飾と表示系の名前だけ — Linux / Windows はモックアップどおりの
自前タイトルバー、macOS は信号機ボタンがあるので OS 標準の装飾を使う。

分岐はもう 3 つある。ウィンドウ装飾（自前タイトルバーか OS 標準か）、表示系の名前、
そして**資格情報の預け先**（`PlatformSecretStore` の実装。Windows = 資格情報マネージャー、
macOS = キーチェーン、Linux = Secret Service）。接続情報 (TOML) の置き場所も
`IPlatformProfile.ProfileDirectory` として OS ごとに決まる。

OS の見分け（`HostPlatform`）と共通部分（`PlatformProfileBase`・`PlatformSecretStore`）は
OS に依らないので共通の `SQLForge.Infrastructure` に置き、選び分けは
`PlatformProfileRegistry` と `SecretStoreRegistry` が引き受ける。見分けのつかない OS では
`UnknownPlatformProfile` と `UnavailableSecretStore` が受けるので、起動はできる
（キーリングが無いぶん、パスワードは都度入力になる）。

どの OS のプロジェクトも `net10.0` のままにしてある。`net10.0-windows` のような OS 付きの
TFM にすると、3 つとも参照する合成ルートが他の OS では組めなくなるため。OS 固有の API を
呼ぶときは、TFM ではなく呼び出し側に `[SupportedOSPlatform]` を付ける。

OS 統合認証（Windows 認証）で名乗るアカウント名も OS ごとに変わるので、
`IPlatformProfile.IntegratedAccountName` と `IntegratedAuthenticationNeedsKerberos` として
同じ仕組みに載せてある（Windows だけが `WindowsIdentity` で上書きする）。
Linux / macOS では Kerberos の設定が要る。整っていない環境では
接続時に SqlClient がその理由を返すので、ダイアログのフッターにそのまま出る。

### OS を増やすとき

触るのは **新しい OS のプロジェクトと合成ルートの 1 行だけ**で、Domain・Application・UI と
既存の OS のプロジェクトは変わらない。

1. `PlatformKind` に OS を足す
2. `src/SQLForge.Infrastructure.<OS>` を作り、`SQLForge.Infrastructure` を参照する
3. `PlatformProfileBase` を継承して、既定と違うところだけ上書きする
4. その OS のキーリングへ預ける `PlatformSecretStore` を実装する
   （外部コマンドごしなら `CommandLineSecretStore` を継承すると足回りが借りられる）
5. `SQLForge.Ui` から新しいプロジェクトを参照し、`Composition/PlatformProfiles` と
   `Composition/SecretStores` の並びへ 1 行ずつ足す
6. `LayerDependencyTests.PlatformAssemblies` に新しいアセンブリ名を足す
   （共通の Infrastructure や他の OS へ混ざり込んだら落ちるようにするため）
