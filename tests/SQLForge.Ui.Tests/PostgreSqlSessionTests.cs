using SQLForge.Domain.Catalog;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.PostgreSql;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// PostgreSQL のカタログの読み方。実サーバー無しで、投げた文面と読み取った値の
/// 対応、そしてデータベースをまたぐときの振る舞いを確かめる。
/// </summary>
public class PostgreSqlSessionTests
{
    private const string ConnectionString = "Host=db.internal;Database=sales_db;Username=analyst_ro";

    [Fact]
    public async Task データベース一覧は共有カタログから読む()
    {
        // pg_database はどのデータベースに繋いでいても読めるので、張り直しは要らない。
        var connection = new FakeDbConnection
        {
            Rows =
            [
                new object?[] { "sales_db", false, true, "ja_JP.UTF-8" },
                new object?[] { "postgres", true, true, null }
            ]
        };

        var databases = await NewSession(connection).ListDatabasesAsync();

        Assert.Equal(PostgreSqlCatalogQueries.Databases, connection.Commands[0].CommandText);
        Assert.Equal(2, databases.Count);
        Assert.Equal("sales_db", databases[0].Name.Value);
        Assert.False(databases[0].IsSystem);
        Assert.True(databases[0].IsAccessible);
        Assert.Equal("ja_JP.UTF-8", databases[0].Collation);
        Assert.True(databases[1].IsSystem);
        Assert.Null(databases[1].Collation);
    }

    [Fact]
    public async Task データベースの作成日時は持たない()
    {
        // PostgreSQL に作成日時に当たる列が無いので、推測せずに「読めない」を返す。
        var connection = new FakeDbConnection { Rows = [new object?[] { "sales_db", false, true, null }] };

        var databases = await NewSession(connection).ListDatabasesAsync();

        Assert.Null(databases[0].CreatedAt);
    }

    [Fact]
    public async Task 他のデータベースを読むときは接続を張り直す()
    {
        // SQL Server の 3 部名に当たる書き方が無いので、繋ぎ先そのものを移す。
        var connection = new FakeDbConnection { Rows = [new object?[] { "public", false, "postgres" }] };

        await NewSession(connection).ListSchemasAsync(new DatabaseName("analytics"));

        Assert.Contains("Database=analytics", connection.ConnectionString, StringComparison.Ordinal);
        Assert.Equal(PostgreSqlCatalogQueries.Schemas, connection.Commands[0].CommandText);
    }

    [Fact]
    public async Task 張り直しても資格情報は落とさない()
    {
        // 接続文字列を DbConnection から読み直すと Npgsql がパスワードを伏せるので、
        // 開いたときの文字列を持ち回っている。落とすと張り直しで認証に失敗する。
        var connection = new FakeDbConnection { Rows = [] };

        await NewSession(connection).ListSchemasAsync(new DatabaseName("analytics"));

        Assert.Contains("Username=analyst_ro", connection.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task テーブル一覧はスキーマをパラメータで渡す()
    {
        var connection = new FakeDbConnection { Rows = [new object?[] { "orders", 1200L }, new object?[] { "audit_log", null }] };

        var tables = await NewSession(connection)
            .ListTablesAsync(new DatabaseName("sales_db"), new SchemaName("public"));

        Assert.Equal(new object?[] { "public" }, connection.Commands[0].Values);
        Assert.Equal(1200L, tables[0].RowCount);

        // ANALYZE が一度も走っていないテーブルは行数が分からない（reltuples = -1）。
        Assert.Null(tables[1].RowCount);
    }

    [Fact]
    public async Task カラム定義は型と鍵の別まで読む()
    {
        var connection = new FakeDbConnection
        {
            Rows =
            [
                new object?[] { "id", 1, "integer", false, true, true },
                new object?[] { "note", 2, "character varying(50)", true, false, false }
            ]
        };

        var columns = await NewSession(connection)
            .ListColumnsAsync(new DatabaseName("sales_db"), new SchemaName("public"), "orders");

        Assert.Equal(new object?[] { "public", "orders" }, connection.Commands[0].Values);
        Assert.Equal("integer", columns[0].DataType);
        Assert.True(columns[0].IsIdentity);
        Assert.True(columns[0].IsPrimaryKey);
        Assert.Equal("character varying(50)", columns[1].DataType);
        Assert.True(columns[1].IsNullable);
    }

    [Fact]
    public void 先頭N行をのぞく文面はLIMITで絞る()
    {
        // PostgreSQL に TOP は無い。識別子は二重引用符で囲む（囲まないと小文字へ畳まれる）。
        var sql = NewSession(new FakeDbConnection())
            .BuildTopRowsQuery(new SchemaName("public"), "Orders", 1000);

        Assert.Equal("SELECT * FROM \"public\".\"Orders\" LIMIT 1000;", sql);
    }

    [Fact]
    public void カタログを読むところまでだと申告する()
    {
        // 画面はこの申告を見て、セキュリティの枝も編集グリッドのメニューも出さない。
        var session = NewSession(new FakeDbConnection());

        Assert.Equal(SessionCapabilities.CatalogOnly, session.Capabilities);
    }

    [Fact]
    public async Task まだ書いていない操作は理由を付けて断る()
    {
        // 黙って空を返すと「権限が無くて 0 件」と見分けが付かない。
        var session = NewSession(new FakeDbConnection());

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => session.ListServerLoginsAsync());

        Assert.Contains("PostgreSQL", exception.Message, StringComparison.Ordinal);
    }

    private static PostgreSqlSession NewSession(FakeDbConnection connection) =>
        new(SeedConnections.Create().First(),
            connection,
            new ServerInfo("PostgreSQL 16", "16.2"),
            ConnectionString);
}
