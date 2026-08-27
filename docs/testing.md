# テスト方針

**テストファースト（テスト駆動開発）を基本とする。**

- 実装コードを書く前に、期待する振る舞いを表すテストを先に書く。テストが失敗することを確認してから、
  それを通す最小限の実装を行う（Red → Green → Refactor）。
- バグを修正する場合も、まず不具合を再現するテストを追加してから修正する。修正後にそのテストが通ることを確認する。
- ユースケース（`SQLForge.Application`）やドメインルール（`SQLForge.Domain`）の変更は、
  UI を経由しない単体テストで先に振る舞いを固定してから着手する。
- 層の依存方向のような構造的な制約を追加・変更する場合も、`LayerDependencyTests` のように
  違反を機械的に検出できるテストを先に用意する。
- テストは [tests/SQLForge.Ui.Tests](../tests/SQLForge.Ui.Tests) に配置し、対象クラス名 + `Tests` の
  命名（例: `SaveConnectionUseCase` → `SaveConnectionUseCaseTests`）に既存の慣習を合わせる。
- テストを後回しにして実装だけ先に進めることは避ける。やむを得ずテストを後追いにした場合は、
  実装直後・同じ変更の中でテストを追加し、テスト無しの状態でコミットを終わらせない。

## ビルド・テスト

```bash
dotnet run --project src/SQLForge.Ui      # アプリを起動
dotnet test                                # 全テスト実行（ヘッドレス描画テストを含む）
```

.NET 10 SDK が必要（`global.json` で固定）。
