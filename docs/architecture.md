# アーキテクチャ

**オニオンアーキテクチャ**を採用する。依存は外側から内側への一方向で、内側の層は外側の層を一切知らない。

```
SQLForge.Domain                    最内層。エンティティと値オブジェクト、ドメインルール。
      ↑                            他の SQLForge.* プロジェクトへの依存を持たない。
SQLForge.Application               ユースケースとポート（インターフェース）。
      ↑                            Domain のみに依存する。
SQLForge.Infrastructure            ポートの実装のうち DBMS にも OS にも依らないもの
      |                            （接続の台帳・接続テスト・ADO.NET 共通の足回り、
      |                             SSH トンネル、
      |                             保存済み接続の TOML 保存とその書き出し・取り込み、
      |                             キーリングの足回り、OS 判定など）。
      ↑
SQLForge.Infrastructure.SqlServer  SQL Server ドライバー実装。
SQLForge.Infrastructure.PostgreSql PostgreSQL ドライバー実装。
      |                            DB 固有パッケージ（Microsoft.Data.SqlClient・Npgsql 等）を
      |                            抱えるのはドライバー専用プロジェクトのみ。
      |                            ドライバーどうしも互いを知らない。
      ↑
SQLForge.Ui                        最外層。Avalonia のビューとビューモデル。
                                    合成ルート（DI 登録）は Composition/AppServices に集約。
```

層ごとの役割:

| プロジェクト | 役割 |
| --- | --- |
| [src/SQLForge.Domain](../src/SQLForge.Domain) | 接続情報のモデルと規則（環境タグ、ドライバー、接続 URL）、カタログのモデル（データベース／スキーマ／テーブル／サーバー情報）、セキュリティのモデル（データベース ユーザー、サーバー ログイン）、クエリ結果のモデル、編集のモデル（編集できる列・先頭 N 行・セルの書き換え・行の追加と削除）、SQL の読み取り（`Sql/` の字句解析器・整形・補完の文脈判定。DB にも UI にも依らない純粋な処理）、ツリーの絞り込みの規則（`Filtering/` の条件と当てはめ） |
| [src/SQLForge.Application](../src/SQLForge.Application) | ユースケース（`Catalog/`・`Connections/`・`Editing/`・`Query/`・`Security/`）とポート（`Abstractions/` の `IDatabaseConnector` / `IDatabaseSession` / `IConnectionProfileRepository` / `IConnectionArchive` / `IConnectionProbe` / `ISecretStore` / `ISshTunnelBroker` / `IPlatformProfile`）。補完のためにカタログを覚える `Catalog/SchemaCache` もここ |
| [src/SQLForge.Infrastructure](../src/SQLForge.Infrastructure) | ポートの DBMS 非依存な実装。`Connections/`（SSH トンネルを含む）・`Platform/`・`Security/`。DBMS にも OS にも依らないパッケージ（`SSH.NET`）を抱えるのはここ |
| [src/SQLForge.Infrastructure.SqlServer](../src/SQLForge.Infrastructure.SqlServer) | SQL Server 専用ドライバー実装。`Microsoft.Data.SqlClient` を抱えるのはここだけ |
| [src/SQLForge.Infrastructure.PostgreSql](../src/SQLForge.Infrastructure.PostgreSql) | PostgreSQL 専用ドライバー実装。`Npgsql` を抱えるのはここだけ。この版はカタログの読み取りまで |
| src/SQLForge.Infrastructure.{Linux,Windows,MacOs} | OS 固有の実装。ウィンドウの体裁（`PlatformProfileBase` の派生）と、資格情報の預け先（`PlatformSecretStore` の派生。Secret Service / 資格情報マネージャー / キーチェーン） |
| [src/SQLForge.Ui](../src/SQLForge.Ui) | Avalonia の View / ViewModel。`Composition/` が合成ルート、`Presentation/` が表示用の変換ロジック |
| [tests/SQLForge.Ui.Tests](../tests/SQLForge.Ui.Tests) | 全層をまたぐテスト。`LayerDependencyTests` が層の境界（依存の向き）をアセンブリ参照から機械的に検証する |

**DB ドライバーは DBMS ごとに独立したプロジェクトにする。** 共通の `SQLForge.Infrastructure` に混ぜると、
DBMS を 1 つ足すたびに全利用者がその依存（SqlClient・Npgsql 等）を引き込むことになるため。
この境界は `LayerDependencyTests`（[tests/SQLForge.Ui.Tests/LayerDependencyTests.cs](../tests/SQLForge.Ui.Tests/LayerDependencyTests.cs)）が
実際にビルドされたアセンブリの参照から機械的に検証している。新しい依存を追加・変更したときは、このテストが通ることを確認する。

**できることの差は例外ではなく申告で表す。** ドライバーごとに実装の進み具合が違うので、
セッションは `SessionCapabilities`（`SQLForge.Domain/Connections`）で自分にできることを申告し、
画面（`MainWindowViewModelFactory`）はそれを見てツリーの枝と右クリックのメニューを出し分ける。
押してから «未対応» と断るのではなく、はじめから出さないため。

エンティティ（`ConnectionProfile`・`DatabaseUserDefinition` など）は常に妥当な状態であることを前提とする。
編集中の値は `ConnectionDraft`・`DatabaseUserDraft` のような Draft 型で保持し、
検証を通してからエンティティへ変換する。

新しい DBMS・OS を追加する手順は [extending.md](extending.md) を参照。
画面まわりのファイル構成は [ui.md](ui.md)、OS ごとの差分は [platform.md](platform.md) を参照。
