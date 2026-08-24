using SQLForge.Domain.Security;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// ツリーのユーザー行の右端に出す補足。SSMS の一覧に合わせ、
/// 種類と、対応づいたログインだけを出す（ロールはプロパティで見る）。
/// </summary>
public static class DatabaseUserDetailFormat
{
    public static string Describe(DatabaseUserDescriptor user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var type = user.Type.DisplayName();

        return string.IsNullOrEmpty(user.LoginName) ? type : $"{type} · {user.LoginName}";
    }
}
