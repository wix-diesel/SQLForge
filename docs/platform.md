# OS の想定

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

新しい OS を追加する手順は [extending.md](extending.md) の「OS を増やすとき」を参照。
