namespace SQLForge.Domain.Security;

/// <summary>権限を持つ主体の種類。GRANT 文の TO 句に出る相手にあたる。</summary>
public enum SecurityPrincipalKind
{
    /// <summary>サーバー ログイン。サーバー スコープの権限を持つ。</summary>
    ServerLogin,

    /// <summary>サーバー ロール。サーバー スコープの権限を持つ。</summary>
    ServerRole,

    /// <summary>データベース ユーザー。データベース スコープの権限を持つ。</summary>
    DatabaseUser,

    /// <summary>データベース ロール。データベース スコープの権限を持つ。</summary>
    DatabaseRole
}

/// <summary>
/// 権限の持ち主 1 人。SSMS の「セキュリティ保護可能なリソース」ページを開いている相手で、
/// ログイン・サーバー ロール・ユーザー・データベース ロールのどれかになる。
/// </summary>
/// <param name="Kind">主体の種類。権限を読む場所（サーバーかデータベースか）もこれで決まる。</param>
/// <param name="Name">主体の名前。</param>
public sealed record SecurityPrincipal(SecurityPrincipalKind Kind, string Name)
{
    /// <summary>
    /// サーバー スコープの主体か。データベースを切り替えずに権限を読み書きする相手になる。
    /// </summary>
    public bool IsServerScoped => Kind.IsServerScoped();

    /// <summary>この主体に付けられるリソースの種類。</summary>
    public IReadOnlyList<SecurableKind> AvailableSecurables => Kind.AvailableSecurables();

    public static SecurityPrincipal ForLogin(ServerLoginName login) =>
        new(SecurityPrincipalKind.ServerLogin, login.Value);

    public static SecurityPrincipal ForServerRole(RoleName role) =>
        new(SecurityPrincipalKind.ServerRole, role.Value);

    public static SecurityPrincipal ForUser(DatabaseUserName user) =>
        new(SecurityPrincipalKind.DatabaseUser, user.Value);

    public static SecurityPrincipal ForDatabaseRole(RoleName role) =>
        new(SecurityPrincipalKind.DatabaseRole, role.Value);
}

/// <summary>
/// 種類だけでわかる性質。まだ名前が決まっていない相手（これから作るユーザーなど）でも
/// 「何を付けられるのか」は決まるので、主体そのものを組まずに引けるようにしておく。
/// </summary>
public static class SecurityPrincipals
{
    /// <summary>サーバー スコープの種類か。</summary>
    public static bool IsServerScoped(this SecurityPrincipalKind kind) =>
        kind is SecurityPrincipalKind.ServerLogin or SecurityPrincipalKind.ServerRole;

    /// <summary>その種類の主体に付けられるリソースの種類。</summary>
    public static IReadOnlyList<SecurableKind> AvailableSecurables(this SecurityPrincipalKind kind) =>
        kind.IsServerScoped() ? Securables.ServerScoped : Securables.DatabaseScoped;
}
