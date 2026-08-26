using System.Collections.ObjectModel;
using SQLForge.Domain.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// 「セキュリティ保護可能なリソース」の一覧の 1 行。選ぶと、下の権限グリッドが
/// この行のものに切り替わる。
/// </summary>
public sealed class SecurableRowViewModel
{
    /// <param name="securable">この行が表すリソース。</param>
    /// <param name="states">
    /// サーバーから読んだ状態。権限の名前から引く。出てこない権限は「指定なし」で並べる。
    /// </param>
    public SecurableRowViewModel(
        SecurableReference securable,
        IReadOnlyDictionary<string, PermissionState>? states = null)
    {
        Securable = securable;

        foreach (var permission in PermissionCatalog.For(securable.Kind))
        {
            var state = states is not null && states.TryGetValue(permission, out var current)
                ? current
                : PermissionState.Revoked;

            Permissions.Add(new PermissionRowViewModel(permission, state));
        }
    }

    public SecurableReference Securable { get; }

    public string DisplayName => Securable.DisplayName;

    public string KindName => Securable.Kind.DisplayName();

    /// <summary>この行に付けられる権限。並びは <see cref="PermissionCatalog"/> のまま。</summary>
    public ObservableCollection<PermissionRowViewModel> Permissions { get; } = [];

    /// <summary>今のグリッドの姿を、そのまま保存に渡せる形で書き出す。</summary>
    public IEnumerable<PermissionEntry> ToEntries() =>
        Permissions.Select(permission => new PermissionEntry(Securable, permission.Permission, permission.State));
}
