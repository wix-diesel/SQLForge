using SQLForge.Application.Catalog;
using SQLForge.Domain.Catalog;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// カタログの並べ方。エンジンの ORDER BY 任せにすると照合順序で並びが変わるので、
/// 並べ替えはユースケース側で必ず行う。
/// </summary>
public class CatalogUseCaseTests
{
    [Fact]
    public async Task データベースは利用者のものが先でシステムのものが後()
    {
        var session = new FakeDatabaseSession
        {
            Databases =
            [
                Database("msdb", isSystem: true),
                Database("sales_db"),
                Database("master", isSystem: true),
                Database("Billing")
            ]
        };

        var databases = await new ListDatabasesUseCase().ExecuteAsync(session);

        Assert.Equal(["Billing", "sales_db", "master", "msdb"], databases.Select(database => database.Name.Value));
    }

    [Fact]
    public async Task スキーマもシステムのものを後ろへ回す()
    {
        var session = new FakeDatabaseSession()
            .WithSchemas("sales_db",
                new SchemaDescriptor(new SchemaName("sys"), IsSystem: true),
                new SchemaDescriptor(new SchemaName("sales")),
                new SchemaDescriptor(new SchemaName("dbo")));

        var schemas = await new ListSchemasUseCase().ExecuteAsync(session, new DatabaseName("sales_db"));

        Assert.Equal(["dbo", "sales", "sys"], schemas.Select(schema => schema.Name.Value));
    }

    [Fact]
    public async Task テーブルは名前順に並ぶ()
    {
        var schema = new SchemaName("dbo");
        var session = new FakeDatabaseSession()
            .WithTables("sales_db", "dbo",
                new TableDescriptor(schema, "orders", 8_400_000),
                new TableDescriptor(schema, "customers", 120),
                new TableDescriptor(schema, "Invoices", 0));

        var tables = await new ListTablesUseCase().ExecuteAsync(session, new DatabaseName("sales_db"), schema);

        Assert.Equal(["customers", "Invoices", "orders"], tables.Select(table => table.Name));
    }

    [Fact]
    public async Task カラムはテーブル定義での並び順のまま出す()
    {
        var schema = new SchemaName("dbo");
        var session = new FakeDatabaseSession()
            .WithColumns("sales_db", "dbo", "orders",
                new ColumnDescriptor("total", 3, "decimal(18, 2)", IsNullable: false, IsIdentity: false, IsPrimaryKey: false),
                new ColumnDescriptor("id", 1, "int", IsNullable: false, IsIdentity: true, IsPrimaryKey: true),
                new ColumnDescriptor("customer_id", 2, "int", IsNullable: false, IsIdentity: false, IsPrimaryKey: false));

        var columns = await new ListColumnsUseCase().ExecuteAsync(session, new DatabaseName("sales_db"), schema, "orders");

        Assert.Equal(["id", "customer_id", "total"], columns.Select(column => column.Name));
    }

    [Fact]
    public async Task ストアドプロシージャも名前順に並ぶ()
    {
        var schema = new SchemaName("dbo");
        var session = new FakeDatabaseSession()
            .WithStoredProcedures("sales_db", "dbo",
                new StoredProcedureDescriptor(schema, "usp_place_order", 3),
                new StoredProcedureDescriptor(schema, "usp_cancel_order", 1),
                new StoredProcedureDescriptor(schema, "Usp_refund_order", 2));

        var procedures = await new ListStoredProceduresUseCase()
            .ExecuteAsync(session, new DatabaseName("sales_db"), schema);

        Assert.Equal(
            ["usp_cancel_order", "usp_place_order", "Usp_refund_order"],
            procedures.Select(procedure => procedure.Name));
    }

    [Fact]
    public async Task ストアドプロシージャのパラメーターは宣言順のまま出す()
    {
        var schema = new SchemaName("dbo");
        var session = new FakeDatabaseSession()
            .WithStoredProcedureParameters("sales_db", "dbo", "usp_place_order",
                new StoredProcedureParameterDescriptor("@total", 3, "decimal(18, 2)", IsOutput: false, HasDefaultValue: false),
                new StoredProcedureParameterDescriptor("@customer_id", 1, "int", IsOutput: false, HasDefaultValue: false),
                new StoredProcedureParameterDescriptor("@order_id", 2, "int", IsOutput: true, HasDefaultValue: false));

        var parameters = await new ListStoredProcedureParametersUseCase()
            .ExecuteAsync(session, new DatabaseName("sales_db"), schema, "usp_place_order");

        Assert.Equal(["@customer_id", "@order_id", "@total"], parameters.Select(parameter => parameter.Name));
    }

    private static DatabaseDescriptor Database(string name, bool isSystem = false) =>
        new(new DatabaseName(name), IsSystem: isSystem);
}
