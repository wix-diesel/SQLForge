# AGENT.md

このファイルは、SQLForge リポジトリで作業するエージェント（Claude Code など）向けのガイドです。

## プロジェクト概要

SQLForge は、Linux で SQL Server（将来的には他の DBMS も）を操作するためのデスクトップアプリ。
UI は Avalonia 12、実行基盤は .NET 10。詳しい機能状況・使い方は [README.md](README.md) を参照。

## 技術スタック

- 言語: C# (`LangVersion` = latest, `Nullable` = enable, `ImplicitUsings` = enable)
- ランタイム: .NET 10（`global.json` で `10.0.100` に固定、`rollForward: latestFeature`）
- UI フレームワーク: Avalonia 12.1 系（`CommunityToolkit.Mvvm` で MVVM）
- DI: `Microsoft.Extensions.DependencyInjection`
- DB アクセス: ADO.NET 系ドライバー（現状は `Microsoft.Data.SqlClient` のみ実装）
- テスト: xUnit v3（`tests/SQLForge.Ui.Tests`）。ヘッドレス描画テストを含む。
  v3 のテストプロジェクトは `OutputType` が `Exe` である必要がある
- 共通ビルド設定は [Directory.Build.props](Directory.Build.props) に集約

## アーキテクチャ

**オニオンアーキテクチャ**を採用する。依存は外側から内側への一方向で、内側の層は外側の層を一切知らない。

```
SQLForge.Domain                    最内層。エンティティと値オブジェクト、ドメインルール。
      ↑                            他の SQLForge.* プロジェクトへの依存を持たない。
SQLForge.Application               ユースケースとポート（インターフェース）。
      ↑                            Domain のみに依存する。
SQLForge.Infrastructure            ポートの実装のうち DBMS にも OS にも依らないもの
      |                            （接続の台帳・接続テスト・ADO.NET 共通の足回り、
      |                             保存済み接続の TOML 保存とその書き出し・取り込み、
      |                             キーリングの足回り、OS 判定など）。
      ↑
SQLForge.Infrastructure.SqlServer  SQL Server ドライバー実装。
      |                            DB 固有パッケージ（Microsoft.Data.SqlClient 等）を
      |                            抱えるのはドライバー専用プロジェクトのみ。
      ↑
SQLForge.Ui                        最外層。Avalonia のビューとビューモデル。
                                    合成ルート（DI 登録）は Composition/AppServices に集約。
```

層ごとの役割:

| プロジェクト | 役割 |
| --- | --- |
| [src/SQLForge.Domain](src/SQLForge.Domain) | 接続情報のモデルと規則（環境タグ、ドライバー、接続 URL）、カタログのモデル（データベース／スキーマ／テーブル／サーバー情報）、セキュリティのモデル（データベース ユーザー、サーバー ログイン）、クエリ結果のモデル、編集のモデル（編集できる列・先頭 N 行・セルの書き換え・行の追加と削除） |
| [src/SQLForge.Application](src/SQLForge.Application) | ユースケース（`Catalog/`・`Connections/`・`Editing/`・`Query/`・`Security/`）とポート（`Abstractions/` の `IDatabaseConnector` / `IDatabaseSession` / `IConnectionProfileRepository` / `IConnectionArchive` / `IConnectionProbe` / `ISecretStore` / `IPlatformProfile`） |
| [src/SQLForge.Infrastructure](src/SQLForge.Infrastructure) | ポートの DBMS 非依存な実装。`Connections/`・`Platform/`・`Security/` |
| [src/SQLForge.Infrastructure.SqlServer](src/SQLForge.Infrastructure.SqlServer) | SQL Server 専用ドライバー実装。`Microsoft.Data.SqlClient` を抱えるのはここだけ |
| src/SQLForge.Infrastructure.{Linux,Windows,MacOs} | OS 固有の実装。ウィンドウの体裁（`PlatformProfileBase` の派生）と、資格情報の預け先（`PlatformSecretStore` の派生。Secret Service / 資格情報マネージャー / キーチェーン） |
| [src/SQLForge.Ui](src/SQLForge.Ui) | Avalonia の View / ViewModel。`Composition/` が合成ルート、`Presentation/` が表示用の変換ロジック |
| [tests/SQLForge.Ui.Tests](tests/SQLForge.Ui.Tests) | 全層をまたぐテスト。`LayerDependencyTests` が層の境界（依存の向き）をアセンブリ参照から機械的に検証する |

**DB ドライバーは DBMS ごとに独立したプロジェクトにする。** 共通の `SQLForge.Infrastructure` に混ぜると、
DBMS を 1 つ足すたびに全利用者がその依存（SqlClient・Npgsql 等）を引き込むことになるため。
この境界は `LayerDependencyTests`（[tests/SQLForge.Ui.Tests/LayerDependencyTests.cs](tests/SQLForge.Ui.Tests/LayerDependencyTests.cs)）が
実際にビルドされたアセンブリの参照から機械的に検証している。新しい依存を追加・変更したときは、このテストが通ることを確認する。

新しい DBMS を追加する手順は [README.md](README.md) の「DBMS を増やすとき」を参照。

## コーディング規約

### 言語

- **コード中のコメント・ドキュメントコメント（`///`）・コミットメッセージなど、文章はすべて日本語で記載する。**
- 識別子（クラス名・メソッド名・変数名など）は既存コードに合わせて英語で命名する。
- 日本語のメソッド名（テストの `Fact` 名など）は既存の慣習に従ってよい（例: `ドメインは外側の層を知らない`）。

### クラス・メソッドのサイズ

- **1 クラスは 300 行以内を推奨する。** ただし、収めるのが困難な場合は 300 行を超えてもよい。
  責務が肥大化している兆候であれば分割を検討するが、無理に分割して可読性を落とさない。
- **1 メソッドは 30 行以内を推奨する。** ただし、収めるのが困難な場合は 30 行を超えてもよい。

### その他

- Nullable 参照型は有効（`Nullable=enable`）。null 許容の意図を型で明示する。
- 層の依存方向を破らない（内側の層から外側の層の型を参照しない）。迷ったら上記のアーキテクチャ表を確認する。
- エンティティ（`ConnectionProfile`・`DatabaseUserDefinition` など）は常に妥当な状態であることを前提とする。
  編集中の値は `ConnectionDraft`・`DatabaseUserDraft` のような Draft 型で保持し、
  検証を通してからエンティティへ変換する。

## テスト方針

**テストファースト（テスト駆動開発）を基本とする。**

- 実装コードを書く前に、期待する振る舞いを表すテストを先に書く。テストが失敗することを確認してから、
  それを通す最小限の実装を行う（Red → Green → Refactor）。
- バグを修正する場合も、まず不具合を再現するテストを追加してから修正する。修正後にそのテストが通ることを確認する。
- ユースケース（`SQLForge.Application`）やドメインルール（`SQLForge.Domain`）の変更は、
  UI を経由しない単体テストで先に振る舞いを固定してから着手する。
- 層の依存方向のような構造的な制約を追加・変更する場合も、`LayerDependencyTests` のように
  違反を機械的に検出できるテストを先に用意する。
- テストは [tests/SQLForge.Ui.Tests](tests/SQLForge.Ui.Tests) に配置し、対象クラス名 + `Tests` の
  命名（例: `SaveConnectionUseCase` → `SaveConnectionUseCaseTests`）に既存の慣習を合わせる。
- テストを後回しにして実装だけ先に進めることは避ける。やむを得ずテストを後追いにした場合は、
  実装直後・同じ変更の中でテストを追加し、テスト無しの状態でコミットを終わらせない。

## ビルド・テスト

```bash
dotnet run --project src/SQLForge.Ui      # アプリを起動
dotnet test                                # 全テスト実行（ヘッドレス描画テストを含む）
```

.NET 10 SDK が必要（`global.json` で固定）。
