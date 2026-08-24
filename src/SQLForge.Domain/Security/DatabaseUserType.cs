namespace SQLForge.Domain.Security;

/// <summary>
/// データベース ユーザーの種類。SSMS の「ユーザーの種類」に対応する。
///
/// 一覧にはサーバーが返すすべての種類が出るが、この版で作り替えられるのは
/// <see cref="DatabaseUserTypes.Editable"/> の 4 つだけ。証明書や非対称キーに
/// マップされたユーザーは、鍵そのものの管理が別に要るので表示だけに留める。
/// </summary>
public enum DatabaseUserType
{
    /// <summary>ログインに対応づいた SQL ユーザー（CREATE USER ... FOR LOGIN）。</summary>
    SqlUserWithLogin,

    /// <summary>ログインを持たない SQL ユーザー（CREATE USER ... WITHOUT LOGIN）。</summary>
    SqlUserWithoutLogin,

    /// <summary>Windows ユーザー。</summary>
    WindowsUser,

    /// <summary>Windows グループ。</summary>
    WindowsGroup,

    /// <summary>証明書にマップされたユーザー。</summary>
    Certificate,

    /// <summary>非対称キーにマップされたユーザー。</summary>
    AsymmetricKey,

    /// <summary>外部プロバイダー（Microsoft Entra ID など）のユーザーまたはグループ。</summary>
    External,

    /// <summary>この版が知らない種類。一覧には出すが編集はさせない。</summary>
    Unknown
}

/// <summary>種類ごとの表示名と、文面の組み立てに要る性質。</summary>
public static class DatabaseUserTypes
{
    /// <summary>この版で追加・編集できる種類。ダイアログの選択肢の並びでもある。</summary>
    public static IReadOnlyList<DatabaseUserType> Editable { get; } =
    [
        DatabaseUserType.SqlUserWithLogin,
        DatabaseUserType.SqlUserWithoutLogin,
        DatabaseUserType.WindowsUser,
        DatabaseUserType.WindowsGroup
    ];

    public static string DisplayName(this DatabaseUserType type) => type switch
    {
        DatabaseUserType.SqlUserWithLogin => "SQL ユーザー（ログインあり）",
        DatabaseUserType.SqlUserWithoutLogin => "SQL ユーザー（ログインなし）",
        DatabaseUserType.WindowsUser => "Windows ユーザー",
        DatabaseUserType.WindowsGroup => "Windows グループ",
        DatabaseUserType.Certificate => "証明書にマップされたユーザー",
        DatabaseUserType.AsymmetricKey => "非対称キーにマップされたユーザー",
        DatabaseUserType.External => "外部ユーザー",
        _ => "不明な種類"
    };

    /// <summary>ログイン名を指定して作る種類か。</summary>
    public static bool RequiresLogin(this DatabaseUserType type) =>
        type is DatabaseUserType.SqlUserWithLogin
            or DatabaseUserType.WindowsUser
            or DatabaseUserType.WindowsGroup;

    /// <summary>この版で追加・編集できる種類か。</summary>
    public static bool IsEditable(this DatabaseUserType type) => Editable.Contains(type);
}
