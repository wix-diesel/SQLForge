using System.Diagnostics.CodeAnalysis;
using SQLForge.Domain.Connections;

namespace SQLForge.Application.Abstractions;

/// <summary>
/// ドライバーから接続口を引く台帳。入っていないドライバーは「未対応」として扱い、
/// 例外ではなく <see cref="TryResolve"/> の false で返す（対応状況は想定内の状態なので）。
/// </summary>
public interface IDatabaseConnectorRegistry
{
    /// <summary>実装が入っているドライバー。接続ダイアログの注意書きに使う。</summary>
    IReadOnlyList<DatabaseDriver> SupportedDrivers { get; }

    bool Supports(DatabaseDriver driver);

    bool TryResolve(DatabaseDriver driver, [NotNullWhen(true)] out IDatabaseConnector? connector);
}
