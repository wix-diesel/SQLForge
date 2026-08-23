using System.Reflection;
using SQLForge.Domain.Catalog;
using SQLForge.Infrastructure.Connections.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 識別子まわりの守り。データベース名は 3 部名として SQL 文へ直に埋めるしかないので、
/// 名前の形と引用符付けの両方で「文を閉じられない」ようにしておく。
/// </summary>
public class SqlServerIdentifierTests
{
    [Fact]
    public void 名前に制御文字は使えない()
    {
        Assert.Throws<ArgumentException>(() => new DatabaseName("sales\ndb"));
        Assert.Throws<ArgumentException>(() => new SchemaName("db\to"));
    }

    [Fact]
    public void 空の名前は作れない()
    {
        Assert.Throws<ArgumentException>(() => new DatabaseName("  "));
        Assert.Throws<ArgumentException>(() => new SchemaName(string.Empty));
    }

    [Fact]
    public void 角括弧を含む名前でも引用符を閉じられない()
    {
        // [db] の中で ] を二重にしないと、そこで識別子が閉じて後続の SQL を差し込まれてしまう。
        var quoted = Quote("sales]; DROP TABLE x --");

        Assert.Equal("[sales]]; DROP TABLE x --]", quoted);
    }

    [Fact]
    public void 長すぎる識別子は弾く()
    {
        Assert.Throws<ArgumentException>(() => Quote(new string('a', 129)));
    }

    /// <summary>引用符付けは internal なので、リフレクション越しに確かめる。</summary>
    private static string Quote(string identifier)
    {
        var type = typeof(SqlServerConnector).Assembly
            .GetType("SQLForge.Infrastructure.Connections.SqlServer.SqlServerIdentifier")!;
        var method = type.GetMethod("Quote", BindingFlags.Public | BindingFlags.Static)!;

        try
        {
            return (string)method.Invoke(null, [identifier])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }
}
