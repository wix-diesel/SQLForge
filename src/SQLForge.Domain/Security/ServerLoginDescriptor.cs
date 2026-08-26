using SQLForge.Domain.Catalog;

namespace SQLForge.Domain.Security;

/// <summary>
/// サーバー上のログイン 1 件。SSMS の [サーバー] → [セキュリティ] → [ログイン] に並ぶもの。
/// </summary>
/// <param name="Name">ログイン名。</param>
/// <param name="Type">ログインの種類（認証方式）。</param>
/// <param name="DefaultDatabase">既定のデータベース。読めない、または持たないときは null。</param>
/// <param name="IsDisabled">無効にされているか（ALTER LOGIN ... DISABLE）。</param>
/// <param name="IsSystem">エンジンが用意したログイン（sa や ## で始まるもの）。編集させない。</param>
public sealed record ServerLoginDescriptor(
    ServerLoginName Name,
    ServerLoginType Type,
    DatabaseName? DefaultDatabase = null,
    bool IsDisabled = false,
    bool IsSystem = false)
{
    /// <summary>所属しているサーバー ロール。public はすべてのログインが持つので含めない。</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// 掛かっているパスワードの規則。SQL Server 認証のログインだけが持ち、
    /// ほかの種類（Windows・証明書）では null。
    /// </summary>
    public ServerLoginPasswordPolicy? PasswordPolicy { get; init; }

    /// <summary>この版で編集・削除してよいか。</summary>
    public bool IsEditable => !IsSystem && Type.IsEditable();
}
