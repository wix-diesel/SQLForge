using System.Diagnostics.CodeAnalysis;
using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 合成ルートに登録された接続口をドライバー別に引けるようにする。
/// DBMS を増やすときは <see cref="IDatabaseConnector"/> の実装を DI へ足すだけでよく、
/// ここは触らない。
/// </summary>
public sealed class DatabaseConnectorRegistry : IDatabaseConnectorRegistry
{
    private readonly Dictionary<string, IDatabaseConnector> _connectors;

    public DatabaseConnectorRegistry(IEnumerable<IDatabaseConnector> connectors)
    {
        ArgumentNullException.ThrowIfNull(connectors);

        _connectors = connectors.ToDictionary(
            connector => connector.Driver.Id,
            StringComparer.OrdinalIgnoreCase);

        // 提示順はドライバー一覧の並びに揃える（接続ダイアログの選択肢と同じ順序）。
        SupportedDrivers = DatabaseDriver.All.Where(driver => _connectors.ContainsKey(driver.Id)).ToList();
    }

    public IReadOnlyList<DatabaseDriver> SupportedDrivers { get; }

    public bool Supports(DatabaseDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);

        return _connectors.ContainsKey(driver.Id);
    }

    public bool TryResolve(DatabaseDriver driver, [NotNullWhen(true)] out IDatabaseConnector? connector)
    {
        ArgumentNullException.ThrowIfNull(driver);

        return _connectors.TryGetValue(driver.Id, out connector);
    }
}
