using SQLForge.Domain.Catalog;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// スキーマの追加・所有者の付け替え・削除の文面。
/// 名前は変えられないので、編集で出るのは所有権の移動だけ。
/// </summary>
public class SqlServerSchemaStatementsTests
{
    [Fact]
    public void スキーマは所有者を添えて作る()
    {
        var statements = SqlServerSchemaStatements.Create(
            new SchemaDefinition(new SchemaName("sales"), "app_user"));

        Assert.Equal(["CREATE SCHEMA [sales] AUTHORIZATION [app_user];"], statements);
    }

    [Fact]
    public void 所有者を指定しなければ文面にも出さない()
    {
        var statements = SqlServerSchemaStatements.Create(new SchemaDefinition(new SchemaName("sales")));

        Assert.Equal(["CREATE SCHEMA [sales];"], statements);
    }

    [Fact]
    public void 所有者を変えたときだけ所有権を移す()
    {
        var original = new SchemaDescriptor(new SchemaName("sales"), Owner: "dbo");

        Assert.Empty(SqlServerSchemaStatements.Alter(
            original,
            new SchemaDefinition(new SchemaName("sales"), "dbo")));

        Assert.Equal(
            ["ALTER AUTHORIZATION ON SCHEMA::[sales] TO [app_user];"],
            SqlServerSchemaStatements.Alter(original, new SchemaDefinition(new SchemaName("sales"), "app_user")));
    }

    [Fact]
    public void 所有者を空にした編集は今のままにする()
    {
        // スキーマは持ち主を空にできない。空欄は「今のまま」の意味にする。
        var original = new SchemaDescriptor(new SchemaName("sales"), Owner: "dbo");

        Assert.Empty(SqlServerSchemaStatements.Alter(original, new SchemaDefinition(new SchemaName("sales"))));
    }

    [Fact]
    public void スキーマはDROP_SCHEMAで消す()
    {
        Assert.Equal("DROP SCHEMA [sales];", SqlServerSchemaStatements.Drop(new SchemaName("sales")));
    }
}
