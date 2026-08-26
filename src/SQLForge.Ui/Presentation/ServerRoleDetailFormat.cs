using SQLForge.Domain.Security;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// ツリーのサーバー ロール行の右端に出す補足。
/// SSMS の一覧に合わせ、所有者とメンバーの数だけを出す。
/// </summary>
public static class ServerRoleDetailFormat
{
    public static string Describe(ServerRoleDescriptor role)
    {
        ArgumentNullException.ThrowIfNull(role);

        var members = $"メンバー {role.Members.Count}";

        return string.IsNullOrEmpty(role.Owner) ? members : $"所有者 {role.Owner} · {members}";
    }
}
