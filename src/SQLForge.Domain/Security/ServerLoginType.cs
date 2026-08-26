namespace SQLForge.Domain.Security;

/// <summary>
/// サーバー ログインの種類。SSMS の「ログイン - 新規作成」の認証方式に対応する。
///
/// 一覧にはサーバーが返すすべての種類が出るが、この版で作り替えられるのは
/// <see cref="ServerLoginTypes.Editable"/> の 3 つだけ。証明書や非対称キーに
/// マップされたログインは、鍵そのものの管理が別に要るので表示だけに留める。
/// </summary>
public enum ServerLoginType
{
    /// <summary>SQL Server 認証のログイン（CREATE LOGIN ... WITH PASSWORD）。</summary>
    SqlLogin,

    /// <summary>Windows 認証のログイン（CREATE LOGIN ... FROM WINDOWS）。</summary>
    WindowsUser,

    /// <summary>Windows 認証のグループ。文面は Windows ユーザーと同じ。</summary>
    WindowsGroup,

    /// <summary>証明書にマップされたログイン。</summary>
    Certificate,

    /// <summary>非対称キーにマップされたログイン。</summary>
    AsymmetricKey,

    /// <summary>外部プロバイダー（Microsoft Entra ID など）のログインまたはグループ。</summary>
    External,

    /// <summary>この版が知らない種類。一覧には出すが編集はさせない。</summary>
    Unknown
}

/// <summary>種類ごとの表示名と、文面の組み立てに要る性質。</summary>
public static class ServerLoginTypes
{
    /// <summary>この版で追加・編集できる種類。ダイアログの選択肢の並びでもある。</summary>
    public static IReadOnlyList<ServerLoginType> Editable { get; } =
    [
        ServerLoginType.SqlLogin,
        ServerLoginType.WindowsUser,
        ServerLoginType.WindowsGroup
    ];

    public static string DisplayName(this ServerLoginType type) => type switch
    {
        ServerLoginType.SqlLogin => "SQL Server 認証のログイン",
        ServerLoginType.WindowsUser => "Windows 認証のログイン",
        ServerLoginType.WindowsGroup => "Windows 認証のグループ",
        ServerLoginType.Certificate => "証明書にマップされたログイン",
        ServerLoginType.AsymmetricKey => "非対称キーにマップされたログイン",
        ServerLoginType.External => "外部ログイン",
        _ => "不明な種類"
    };

    /// <summary>パスワードとパスワード ポリシーを持つ種類か。</summary>
    public static bool RequiresPassword(this ServerLoginType type) => type is ServerLoginType.SqlLogin;

    /// <summary>
    /// Windows から取り込む種類か。ユーザーとグループの別は SID から決まるので、
    /// どちらも文面は CREATE LOGIN ... FROM WINDOWS で変わらない。
    /// </summary>
    public static bool IsWindows(this ServerLoginType type) =>
        type is ServerLoginType.WindowsUser or ServerLoginType.WindowsGroup;

    /// <summary>この版で追加・編集できる種類か。</summary>
    public static bool IsEditable(this ServerLoginType type) => Editable.Contains(type);
}
