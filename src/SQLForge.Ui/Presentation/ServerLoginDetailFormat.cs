using SQLForge.Domain.Security;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// ツリーのログイン行の右端に出す補足。SSMS の一覧に合わせ、認証方式と既定のデータベース、
/// それに無効かどうかだけを出す（ロールはプロパティで見る）。
/// </summary>
public static class ServerLoginDetailFormat
{
    public static string Describe(ServerLoginDescriptor login)
    {
        ArgumentNullException.ThrowIfNull(login);

        var parts = new List<string> { login.Type.DisplayName() };

        if (login.DefaultDatabase is { } database)
        {
            parts.Add(database.Value);
        }

        // 無効なログインは繋げないので、開かなくても分かるようにしておく。
        if (login.IsDisabled)
        {
            parts.Add("無効");
        }

        return string.Join(" · ", parts);
    }
}
