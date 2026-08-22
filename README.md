# SQLForge

Linux で SQL Server を操作するためのアプリ。

UI デザインは [`design/`](design/README.md) にある。実装は Avalonia 11 + .NET 8。

## 現在の状態

**フェーズ 1 の 1 画面目 — 起動時の接続ダイアログ**（`design/Connect.dc.html` に対応）だけが実装済み。

アプリを起動すると接続ダイアログが開く。保存済み接続の一覧・検索、接続情報の入力と検証、
環境タグ、読み取り専用モードのトグル、接続 URL の組み立てまでが動く。

**DB へは接続しない。** ドライバーを 1 つも入れていないため、「接続をテスト」と「接続」は
Infrastructure 層の疑似実装（`SimulatedConnectionProbe`）が応答する。実装済みの入出力と、
差し替え待ちの部分は次のとおり。

| 部分 | 現状 |
| --- | --- |
| 保存済み接続の永続化 | プロセス内のみ（`InMemoryConnectionProfileRepository`）。TOML への保存は未実装 |
| 資格情報 | プロセス内のみ（`InMemorySecretStore`）。Secret Service / 資格情報マネージャー / キーチェーンは未接続 |
| 接続テスト・接続 | 疑似応答。実際の通信は行わない |
| SSH トンネル / TLS / 詳細設定タブ | タブは出るが中身は未実装（TLS の要求レベルだけ「一般」タブにある） |
| メインウィンドウ・設定画面 | 未着手（フェーズ 1 の残り） |

デザインからの意図的な差分が 1 点ある。モックアップでは一覧の選択行に緑の接続中ドットが付くが、
接続機構がない状態でそれを出すと嘘になるので、代わりに読み取り専用で開く接続へ盾のアイコンを出している。

## 動かす

```sh
dotnet run --project src/SQLForge.Ui      # 起動すると接続ダイアログが開く
dotnet test                                # ヘッドレス描画テストを含む
```

.NET 8 SDK が要る。Linux では X11（Wayland では XWayland 経由）で動く。

## 構成

オニオンアーキテクチャ。依存は外から内へ一方向で、内側は外側を知らない。

```
SQLForge.Domain          接続情報のモデルと規則（環境タグ、ドライバー、接続 URL の組み立て）
      ↑
SQLForge.Application     ユースケースとポート（IConnectionProfileRepository / IConnectionProbe /
                         ISecretStore / IPlatformProfile）。入力は ConnectionDraft で受ける
      ↑
SQLForge.Infrastructure  ポートの実装。現状はすべて差し替え前提の疑似実装
      ↑
SQLForge.Ui              Avalonia のビューとビューモデル。合成ルートは Composition/AppServices
```

エンティティ（`ConnectionProfile`）は常に妥当である前提なので、編集中の値は
`ConnectionDraft` で持ち、検証を通ってからエンティティへ変換する。

ドライバーを入れるときに触るのは Infrastructure だけで、Domain と Application と UI は変わらない。

### 画面まわり

| ファイル | 中身 |
| --- | --- |
| `Themes/Tokens.axaml` | デザイントークン（`design/avalonia/Tokens.axaml` 由来）。色はテーマ別、寸法は共通 |
| `Themes/Controls.axaml` | コントロールテーマ。ControlTheme と Style を 1 ファイルに収めるため `Styles` として書く |
| `Themes/Icons.axaml` | モックアップの SVG パスをそのまま持ってきた 16px のアイコン |
| `Views/ConnectWindow.axaml` | 起動時のウィンドウ。左ペイン・タブ・フッターを組み合わせる |

色はすべてトークン経由の `DynamicResource` で引いているので、ライトテーマは
`Application.Current.RequestedThemeVariant` を変えるだけで切り替わる（設定画面はまだない）。

フォントは同梱していない。OS のフォントへ順にフォールバックする指定にしてあるので、
配布時は `src/SQLForge.Ui/Assets/Fonts/README.md` のとおり IBM Plex Sans JP と
JetBrains Mono を埋め込む。

## OS の想定

初期版の対象は Linux だが、Windows と macOS でもそのまま起動できるようにしてある。
OS 依存は `IPlatformProfile`（Infrastructure の `PlatformProfile`）に閉じ込めてあり、
現状の分岐はウィンドウ装飾だけ — Linux / Windows はモックアップどおりの自前タイトルバー、
macOS は信号機ボタンがあるので OS 標準の装飾を使う。
