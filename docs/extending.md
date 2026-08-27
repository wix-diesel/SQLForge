# 拡張のしかた

## DBMS を増やすとき

PostgreSQL などを足すときに触るのは **新しいドライバープロジェクトと合成ルートの 1 行だけ**で、
Domain・Application・UI と既存のドライバーは変わらない。

1. `src/SQLForge.Infrastructure.PostgreSql`（例）を作り、`SQLForge.Infrastructure` を参照して
   ドライバーのパッケージ（`Npgsql` など）を入れる。SQL Server 側がそのまま雛形になる
2. `IDatabaseConnector` の実装を書く（接続を開いてサーバーの素性を読む）
3. `AdoDatabaseSession` を継承してカタログの読み方を 3 つ埋め
   （`ReadDatabasesAsync` / `ReadSchemasAsync` / `ReadTablesAsync`）、
   実行先の切り替え方（`SwitchDatabaseAsync`）を書く。
   クエリの結果を読むところは `AdoDatabaseSession` が持っているので触らなくてよい。
   編集グリッドも同じで、埋めるのは列の素性（`ReadEditableColumnsAsync`）と文面の組み立て
   （`BuildTopRowsSelect` / `BuildCellUpdate`）だけ。行数の絞り方（`TOP` と `LIMIT`）と
   識別子の引用符がエンジンごとに違うため
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

## OS を増やすとき

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

OS ごとの現状の差分は [platform.md](platform.md) を参照。
