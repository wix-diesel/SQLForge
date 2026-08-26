namespace SQLForge.Domain.Security;

/// <summary>
/// セキュリティ保護可能なリソースの種類。SSMS の「セキュリティ保護可能なリソースの追加」で
/// 選べる種類のうち、この版が扱えるものだけを並べる。
///
/// GRANT 文のクラス指定（<c>ON SCHEMA::[dbo]</c> など）にそのまま写るので、
/// 種類ごとの前置きは <see cref="Securables.ClassPrefix"/> が持つ。
/// </summary>
public enum SecurableKind
{
    /// <summary>サーバーそのもの。文面にクラスの前置きを持たない（GRANT ... TO ...）。</summary>
    Server,

    /// <summary>ログイン。サーバー スコープ。</summary>
    Login,

    /// <summary>データベース。そのデータベースの中にいる主体から指す。</summary>
    Database,

    /// <summary>スキーマ。データベース スコープ。</summary>
    Schema,

    /// <summary>テーブル。データベース スコープで、スキーマで修飾する。</summary>
    Table,

    /// <summary>ストアド プロシージャ。データベース スコープで、スキーマで修飾する。</summary>
    StoredProcedure
}

/// <summary>種類ごとの表示名と、文面の組み立てに要る性質。</summary>
public static class Securables
{
    /// <summary>
    /// サーバー スコープの主体（ログイン・サーバー ロール）に付けられる種類。
    ///
    /// データベースは入らない。ログインにデータベースの権限を与えるには、そのデータベースに
    /// ユーザーを作って（ユーザー マッピング）そのユーザーへ付けることになるので、
    /// 権限そのものはデータベース スコープの側で扱う。
    /// </summary>
    public static IReadOnlyList<SecurableKind> ServerScoped { get; } =
    [
        SecurableKind.Server,
        SecurableKind.Login
    ];

    /// <summary>データベース スコープの主体（ユーザー・データベース ロール）に付けられる種類。</summary>
    public static IReadOnlyList<SecurableKind> DatabaseScoped { get; } =
    [
        SecurableKind.Database,
        SecurableKind.Schema,
        SecurableKind.Table,
        SecurableKind.StoredProcedure
    ];

    public static string DisplayName(this SecurableKind kind) => kind switch
    {
        SecurableKind.Server => "サーバー",
        SecurableKind.Login => "ログイン",
        SecurableKind.Database => "データベース",
        SecurableKind.Schema => "スキーマ",
        SecurableKind.Table => "テーブル",
        SecurableKind.StoredProcedure => "ストアド プロシージャ",
        _ => "不明な種類"
    };

    /// <summary>
    /// GRANT 文のクラス指定。サーバーそのものだけはクラスを持たず、
    /// <c>GRANT VIEW ANY DATABASE TO ...</c> のように相手を書かない。
    /// </summary>
    public static string? ClassPrefix(this SecurableKind kind) => kind switch
    {
        SecurableKind.Server => null,
        SecurableKind.Login => "LOGIN",
        SecurableKind.Database => "DATABASE",
        SecurableKind.Schema => "SCHEMA",
        // テーブルとストアド プロシージャはどちらも OBJECT クラス。
        SecurableKind.Table or SecurableKind.StoredProcedure => "OBJECT",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "知らない種類です。")
    };

    /// <summary>スキーマで修飾する種類か。</summary>
    public static bool IsSchemaQualified(this SecurableKind kind) =>
        kind is SecurableKind.Table or SecurableKind.StoredProcedure;

    /// <summary>
    /// サーバー スコープの種類か。データベース スコープの主体には付けられない
    /// （<see cref="SecurableKind.Database"/> はどちらからも指せるので、どちらでもない）。
    /// </summary>
    public static bool IsServerOnly(this SecurableKind kind) =>
        kind is SecurableKind.Server or SecurableKind.Login;
}
