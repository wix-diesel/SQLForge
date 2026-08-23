using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;
using SQLForge.Infrastructure.Connections;
using SQLForge.Infrastructure.Linux;
using SQLForge.Infrastructure.MacOs;
using SQLForge.Infrastructure.Platform;
using SQLForge.Infrastructure.SqlServer;
using SQLForge.Infrastructure.Windows;
using SQLForge.Ui.Composition;
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

    /// <summary>OS 依存を抱えるプロジェクト。OS を増やすときはここへ足す。</summary>
    private static readonly string[] PlatformAssemblies =
    [
        "SQLForge.Infrastructure.Linux",
        "SQLForge.Infrastructure.Windows",
        "SQLForge.Infrastructure.MacOs"
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
    public void 共通のインフラはOS依存を抱えない()
    {
        // OS ごとの体裁は OS ごとに別プロジェクトへ置く。ここへ混ぜると、
        // OS を 1 つ足すたびに全利用者がその分岐を引き込むことになる。
        Assert.DoesNotContain(
            ReferencesOf<PlatformProfileBase>(),
            name => PlatformAssemblies.Contains(name, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(typeof(LinuxPlatformProfile))]
    [InlineData(typeof(WindowsPlatformProfile))]
    [InlineData(typeof(MacOsPlatformProfile))]
    public void OSのプロジェクトどうしは互いを知らない(Type profile)
    {
        // どれか 1 つが他の OS の体裁を覗くと、分けた意味が無くなる
        // （自分自身は参照一覧に出ないので、他の 2 つが出ないことだけを見ればよい）。
        Assert.DoesNotContain(
            ReferencesOf(profile),
            name => PlatformAssemblies.Contains(name, StringComparer.Ordinal));
    }

    [Fact]
    public void ドライバーのプロジェクトだけがドライバーを抱える()
    {
        // 上が「参照が無いこと」の確認なので、検出できることをここで裏取りする。
        Assert.Contains("Microsoft.Data.SqlClient", ReferencesOf<SqlServerConnector>());
    }

    [Fact]
    public void 合成ルートだけがOSごとの実装を知る()
    {
        // OS の選び分けは合成ルート（PlatformProfiles）の仕事なので、
        // 3 つとも参照しているのはここだけでよい。
        Assert.All(PlatformAssemblies, name => Assert.Contains(name, ReferencesOf(typeof(PlatformProfiles))));
    }

    private static IEnumerable<string> ReferencesOf<T>() => ReferencesOf(typeof(T));

    private static IEnumerable<string> ReferencesOf(Type type) =>
        type.Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Order(StringComparer.Ordinal);
}
