using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 層とプロジェクトの境目が崩れていないこと。
///
/// 依存の向きは口約束では守られないので、組み上がったアセンブリの参照から確かめる
/// （コンパイラは実際に使われた参照だけを残すため、「使ってしまった」ものはここに出る）。
/// </summary>
public class LayerDependencyTests
{
    /// <summary>DB ドライバーを持ち込むパッケージ。増やすときはここへ足す。</summary>
    private static readonly string[] DriverAssemblies =
    [
        "Microsoft.Data.SqlClient",
        "Npgsql",
        "MySqlConnector",
        "Microsoft.Data.Sqlite",
        "ClickHouse.Client"
    ];

    [Fact]
    public void ドメインは外側の層を知らない()
    {
        Assert.DoesNotContain(
            ReferencesOf<DatabaseDriver>(),
            name => name.StartsWith("SQLForge.", StringComparison.Ordinal));
    }

    [Fact]
    public void アプリケーションはドメインだけに依存する()
    {
        var sqlforge = ReferencesOf<IDatabaseSession>().Where(name => name.StartsWith("SQLForge.", StringComparison.Ordinal));

        Assert.Equal(["SQLForge.Domain"], sqlforge);
    }

    [Fact]
    public void 共通のインフラはDBドライバーを抱えない()
    {
        // ドライバーはそれぞれ別プロジェクトに置く。ここへ混ぜると、
        // 1 つ DBMS を足すたびに全利用者がその依存を引き込むことになる。
        Assert.DoesNotContain(
            ReferencesOf<AdoDatabaseSession>(),
            name => DriverAssemblies.Contains(name, StringComparer.Ordinal));
    }

    [Fact]
    public void ドライバーのプロジェクトだけがドライバーを抱える()
    {
        // 上の 3 つが「参照が無いこと」の確認なので、検出できることをここで裏取りする。
        Assert.Contains("Microsoft.Data.SqlClient", ReferencesOf<SqlServerConnector>());
    }

    private static IEnumerable<string> ReferencesOf<T>() =>
        typeof(T).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Order(StringComparer.Ordinal);
}
