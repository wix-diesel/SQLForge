# AGENT.md

このファイルは、SQLForge リポジトリで作業するエージェント（Claude Code など）向けのガイドです。

## プロジェクト概要

SQLForge は、Linux で SQL Server（将来的には他の DBMS も）を操作するためのデスクトップアプリ。
UI は Avalonia 12、実行基盤は .NET 10。詳しい機能状況・使い方は [README.md](README.md) と
[docs/](docs/) を参照。

## 技術スタック

- 言語: C# (`LangVersion` = latest, `Nullable` = enable, `ImplicitUsings` = enable)
- ランタイム: .NET 10（`global.json` で `10.0.100` に固定、`rollForward: latestFeature`）
- UI フレームワーク: Avalonia 12.1 系（`CommunityToolkit.Mvvm` で MVVM）
- DI: `Microsoft.Extensions.DependencyInjection`
- DB アクセス: ADO.NET 系ドライバー（現状は `Microsoft.Data.SqlClient` のみ実装）
- テスト: xUnit v3（`tests/SQLForge.Ui.Tests`）。ヘッドレス描画テストを含む。
  v3 のテストプロジェクトは `OutputType` が `Exe` である必要がある
- 共通ビルド設定は [Directory.Build.props](Directory.Build.props) に集約

## ドキュメント

作業前に読むべきドキュメントは [docs/](docs/) に分割してある。

| ドキュメント | 内容 |
| --- | --- |
| [docs/architecture.md](docs/architecture.md) | オニオンアーキテクチャ、層ごとの役割、プロジェクト構成 |
| [docs/coding-guidelines.md](docs/coding-guidelines.md) | コーディング規約（言語、クラス・メソッドのサイズなど） |
| [docs/testing.md](docs/testing.md) | テスト方針（テストファースト）とビルド・テストコマンド |
| [docs/extending.md](docs/extending.md) | 新しい DBMS・OS を追加する手順 |
| [docs/platform.md](docs/platform.md) | OS ごとの差分（ウィンドウ装飾・資格情報の預け先など） |
| [docs/ui.md](docs/ui.md) | 画面まわりのファイル構成 |
| [docs/features.md](docs/features.md) | 現在動く機能の一覧と状態 |
| [docs/connections.md](docs/connections.md) | 接続（SQL Server・OS 統合認証・SSH トンネル・TLS・保存） |
| [docs/query-editing.md](docs/query-editing.md) | クエリ実行とテーブル編集の仕様 |

とくに変更を加える前に、[docs/architecture.md](docs/architecture.md) の層の依存方向と、
[docs/coding-guidelines.md](docs/coding-guidelines.md) / [docs/testing.md](docs/testing.md) の
規約・テストファーストの方針を確認すること。

## ビルド・テスト

```bash
dotnet run --project src/SQLForge.Ui      # アプリを起動
dotnet test                                # 全テスト実行（ヘッドレス描画テストを含む）
```

.NET 10 SDK が必要（`global.json` で固定）。
