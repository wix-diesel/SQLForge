using SQLForge.Domain.Security;

namespace SQLForge.Infrastructure.SqlServer;

/// <summary>
/// 権限の変更の文面。主体名・リソース名は識別子なので
/// <see cref="SqlServerIdentifier.Quote"/> を通し、権限の名前は識別子ではないので
/// 引用符では囲めない。囲めない以上、この版が知っている権限
/// （<see cref="PermissionCatalog"/>）だけを文面に出す。
/// </summary>
internal static class SqlServerPermissionStatements
{
    /// <summary>
    /// 変わったところだけを並べる。<paramref name="desired"/> に出てくる「相手 × 権限」だけを見て、
    /// そこに無いものは触らない（この版が知らない権限を黙って落とさないため）。
    /// </summary>
    public static IReadOnlyList<string> Changes(
        SecurityPrincipal principal,
        IReadOnlyList<PermissionEntry> original,
        IReadOnlyList<PermissionEntry> desired)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(desired);

        var grantee = SqlServerIdentifier.Quote(principal.Name);
        var statements = new List<string>();

        foreach (var entry in desired)
        {
            // 検証を通っていれば当たらないが、文面へ出す手前でもう一度見る。
            if (!PermissionCatalog.IsKnown(entry.Securable.Kind, entry.Permission))
            {
                continue;
            }

            var before = original.FirstOrDefault(entry.IsSameTarget)?.State ?? PermissionState.Revoked;

            if (before == entry.State)
            {
                continue;
            }

            statements.AddRange(Change(grantee, entry, before));
        }

        return statements;
    }

    private static IEnumerable<string> Change(string grantee, PermissionEntry entry, PermissionState before)
    {
        var target = $"{entry.Permission}{On(entry.Securable)}";

        // 付与する権利を取り上げるには、GRANT OPTION を先に外すしかない
        // （GRANT を出し直しても付いたままになる）。相手へ渡ったぶんも一緒に外れる。
        if (before == PermissionState.GrantedWithGrantOption && entry.State == PermissionState.Granted)
        {
            yield return $"REVOKE GRANT OPTION FOR {target} FROM {grantee} CASCADE;";
        }

        // 付与する権利が付いたままの権限は、CASCADE 無しでは外せない（サーバーが弾く）。
        var cascade = before == PermissionState.GrantedWithGrantOption ? " CASCADE" : string.Empty;

        yield return entry.State switch
        {
            PermissionState.Granted => $"GRANT {target} TO {grantee};",
            PermissionState.GrantedWithGrantOption => $"GRANT {target} TO {grantee} WITH GRANT OPTION;",
            PermissionState.Denied => $"DENY {target} TO {grantee}{cascade};",
            _ => $"REVOKE {target} FROM {grantee}{cascade};"
        };
    }

    /// <summary>
    /// リソースの指定。サーバーそのものだけはクラスを持たず、相手を書かない
    /// （GRANT VIEW ANY DATABASE TO ... のように、権限だけで意味が通る）。
    /// </summary>
    private static string On(SecurableReference securable)
    {
        if (securable.Kind.ClassPrefix() is not { } prefix)
        {
            return string.Empty;
        }

        var name = securable.Schema is { } schema
            ? $"{SqlServerIdentifier.Quote(schema)}.{SqlServerIdentifier.Quote(securable.Name)}"
            : SqlServerIdentifier.Quote(securable.Name);

        return $" ON {prefix}::{name}";
    }
}
