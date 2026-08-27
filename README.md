# SQLForge

Linux で SQL Server を操作するためのアプリ。

UI デザインは [`design/`](design/README.md) にある。実装は Avalonia 12 + .NET 10。

## 現在の状態

**フェーズ 1 のうち、接続ダイアログ・オブジェクトエクスプローラー・クエリ実行・テーブルの編集**が動く。
機能ごとの詳しい状態は [docs/features.md](docs/features.md) を参照。

## 動かす

```sh
dotnet run --project src/SQLForge.Ui      # 起動すると接続ダイアログが開く
dotnet test                                # ヘッドレス描画テストを含む
```

.NET 10 SDK が要る（`global.json` で 10.0 系に固定している）。
Linux では X11（Wayland では XWayland 経由）で動く。

Avalonia は 12.1 系を使う。net10.0 向けアセットが同梱されている。

## ドキュメント

より詳しい説明は [docs/](docs/) に分割してある。

| ドキュメント | 内容 |
| --- | --- |
| [docs/features.md](docs/features.md) | 現在動く機能の一覧と状態、デザインからの意図的な差分 |
| [docs/connections.md](docs/connections.md) | SQL Server への接続、OS 統合認証、SSH トンネル、TLS の証明書指定、詳細設定、接続情報の保存・削除・書き出し・取り込み |
| [docs/query-editing.md](docs/query-editing.md) | クエリの実行、テーブルの先頭 100 行の編集、行の追加と削除 |
| [docs/architecture.md](docs/architecture.md) | オニオンアーキテクチャと層ごとの役割 |
| [docs/extending.md](docs/extending.md) | 新しい DBMS・OS を追加する手順 |
| [docs/platform.md](docs/platform.md) | OS ごとの差分（ウィンドウ装飾・資格情報の預け先など） |
| [docs/ui.md](docs/ui.md) | 画面まわりのファイル構成 |
| [docs/coding-guidelines.md](docs/coding-guidelines.md) | コーディング規約 |
| [docs/testing.md](docs/testing.md) | テスト方針 |

エージェント（Claude Code など）向けの作業ガイドは [AGENT.md](AGENT.md) を参照。
