using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SQLForge.Application.Security;

namespace SQLForge.Ui.ViewModels.Security;

/// <summary>
/// 「ユーザー マッピング」ページの 1 行。データベース 1 つぶんで、
/// チェックを付けるとそのデータベースにユーザーができる。
/// </summary>
public sealed partial class LoginUserMappingRowViewModel : ObservableObject
{
    private readonly string _loginName;
    private bool _rolesLoaded;

    [ObservableProperty] private bool _isMapped;
    [ObservableProperty] private string _userName;
    [ObservableProperty] private string _defaultSchema;

    /// <param name="database">この行が表すデータベース。</param>
    /// <param name="loginName">対応づけるログイン。ユーザー名を書かなかったときの既定になる。</param>
    /// <param name="mapping">すでに対応づいているならその姿。まだなら null。</param>
    public LoginUserMappingRowViewModel(
        string database,
        string loginName,
        LoginUserMappingDraft? mapping = null)
    {
        Database = database;
        _loginName = loginName;
        _isMapped = mapping?.IsMapped ?? false;
        _userName = mapping?.UserName ?? string.Empty;
        _defaultSchema = mapping?.DefaultSchema ?? string.Empty;
        CurrentRoles = mapping?.Roles ?? [];
    }

    public string Database { get; }

    /// <summary>サーバーから読んだ時点での所属ロール。ロール一覧の初期のチェックに使う。</summary>
    public IReadOnlyList<string> CurrentRoles { get; }

    /// <summary>
    /// このデータベースのロール。行を選んだときに初めて読む
    /// （すべてのデータベースぶんを先に読むと、開くだけで何十回も照会することになる）。
    /// </summary>
    public ObservableCollection<RoleChoiceViewModel> Roles { get; } = [];

    /// <summary>ロールの候補を 1 度だけ読む。</summary>
    public async Task EnsureRolesAsync(
        Func<CancellationToken, Task<IReadOnlyList<string>>> load,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(load);

        if (_rolesLoaded)
        {
            return;
        }

        var roles = await load(cancellationToken).ConfigureAwait(true);

        // 読めたことを先に立てる。同じデータベースを何度も選び直しても読み直さない。
        _rolesLoaded = true;

        foreach (var role in roles)
        {
            Roles.Add(new RoleChoiceViewModel(role, CurrentRoles.Contains(role, StringComparer.OrdinalIgnoreCase)));
        }
    }

    public LoginUserMappingDraft ToDraft() =>
        new()
        {
            Database = Database,
            IsMapped = IsMapped,
            UserName = UserName,
            DefaultSchema = DefaultSchema,
            // ロールをまだ読んでいない行では、読んだときの姿をそのまま残す
            // （見てもいないページで所属を外さない）。
            Roles = _rolesLoaded
                ? Roles.Where(role => role.IsMember).Select(role => role.Name).ToList()
                : CurrentRoles
        };

    /// <summary>
    /// チェックを付けた直後は、SSMS と同じくログイン名をユーザー名の初期値にする。
    /// </summary>
    partial void OnIsMappedChanged(bool value)
    {
        if (value && UserName.Length == 0)
        {
            UserName = _loginName;
        }
    }
}
