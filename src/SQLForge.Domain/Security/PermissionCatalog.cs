namespace SQLForge.Domain.Security;

/// <summary>
/// リソースの種類ごとに、グリッドへ並べる権限の名前。SSMS の権限グリッドが出す一覧のうち、
/// この版で扱うぶんを写したもの。
///
/// 権限の名前は識別子ではないので引用符では囲めず、GRANT 文へ素のまま書くしかない。
/// そのため <see cref="IsKnown"/> を通らない名前は文面へ出さない、という約束にしてある
/// （UI はこの一覧からしか選ばせないが、経路をここ 1 本に絞っておく）。
/// </summary>
public static class PermissionCatalog
{
    private static readonly string[] ServerPermissions =
    [
        "ALTER ANY DATABASE",
        "ALTER ANY LOGIN",
        "ALTER ANY SERVER ROLE",
        "CONNECT SQL",
        "CONTROL SERVER",
        "CREATE ANY DATABASE",
        "SHUTDOWN",
        "VIEW ANY DATABASE",
        "VIEW ANY DEFINITION",
        "VIEW SERVER STATE"
    ];

    private static readonly string[] LoginPermissions =
    [
        "ALTER",
        "CONTROL",
        "IMPERSONATE",
        "VIEW DEFINITION"
    ];

    private static readonly string[] DatabasePermissions =
    [
        "ALTER",
        "ALTER ANY ROLE",
        "ALTER ANY SCHEMA",
        "ALTER ANY USER",
        "BACKUP DATABASE",
        "CONNECT",
        "CONTROL",
        "CREATE PROCEDURE",
        "CREATE SCHEMA",
        "CREATE TABLE",
        "CREATE VIEW",
        "DELETE",
        "EXECUTE",
        "INSERT",
        "SELECT",
        "SHOWPLAN",
        "TAKE OWNERSHIP",
        "UPDATE",
        "VIEW DEFINITION"
    ];

    private static readonly string[] SchemaPermissions =
    [
        "ALTER",
        "CONTROL",
        "DELETE",
        "EXECUTE",
        "INSERT",
        "REFERENCES",
        "SELECT",
        "TAKE OWNERSHIP",
        "UPDATE",
        "VIEW CHANGE TRACKING",
        "VIEW DEFINITION"
    ];

    private static readonly string[] TablePermissions =
    [
        "ALTER",
        "CONTROL",
        "DELETE",
        "INSERT",
        "REFERENCES",
        "SELECT",
        "TAKE OWNERSHIP",
        "UPDATE",
        "VIEW CHANGE TRACKING",
        "VIEW DEFINITION"
    ];

    private static readonly string[] StoredProcedurePermissions =
    [
        "ALTER",
        "CONTROL",
        "EXECUTE",
        "TAKE OWNERSHIP",
        "VIEW DEFINITION"
    ];

    /// <summary>その種類のリソースに付けられる権限の一覧。名前順に並んでいる。</summary>
    public static IReadOnlyList<string> For(SecurableKind kind) => kind switch
    {
        SecurableKind.Server => ServerPermissions,
        SecurableKind.Login => LoginPermissions,
        SecurableKind.Database => DatabasePermissions,
        SecurableKind.Schema => SchemaPermissions,
        SecurableKind.Table => TablePermissions,
        SecurableKind.StoredProcedure => StoredProcedurePermissions,
        _ => []
    };

    /// <summary>
    /// この版が知っている権限か。サーバーから読んだ権限には、ここに無いものも混じりうる
    /// （新しいバージョンで増えた権限など）。読むだけなら妨げないが、文面には出さない。
    /// </summary>
    public static bool IsKnown(SecurableKind kind, string permission) =>
        For(kind).Contains(permission, StringComparer.OrdinalIgnoreCase);
}
