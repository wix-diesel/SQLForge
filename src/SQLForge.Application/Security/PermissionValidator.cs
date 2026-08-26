using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// 権限編集の入力検査。権限の名前は識別子ではなく引用符で囲めないので、
/// 文面へ出す前にこの版が知っているものかどうかをここで見る。
/// </summary>
public static class PermissionValidator
{
    public const string DatabaseField = "database";
    public const string PermissionsField = "permissions";

    public static SecurityValidationResult Validate(PermissionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new Dictionary<string, string>();
        ValidateScope(errors, draft);
        ValidateEntries(errors, draft);

        return errors.Count == 0 ? SecurityValidationResult.Valid : new SecurityValidationResult(errors);
    }

    /// <summary>
    /// データベース スコープの主体には居場所が要る。権限の読み書きは
    /// そのデータベースの中でしか行えない（GRANT は 3 部名で書けない）。
    /// </summary>
    private static void ValidateScope(IDictionary<string, string> errors, PermissionDraft draft)
    {
        if (!draft.Principal.IsServerScoped && draft.Database is null)
        {
            errors[DatabaseField] = "権限を変更するデータベースが決まっていません。";
        }
    }

    private static void ValidateEntries(IDictionary<string, string> errors, PermissionDraft draft)
    {
        var available = draft.Principal.AvailableSecurables;

        foreach (var entry in draft.Entries)
        {
            var kind = entry.Securable.Kind;

            if (!available.Contains(kind))
            {
                SecurityNameRules.Set(
                    errors,
                    PermissionsField,
                    $"{kind.DisplayName()} には、この主体の権限を付けられません。");

                continue;
            }

            if (!PermissionCatalog.IsKnown(kind, entry.Permission))
            {
                SecurityNameRules.Set(
                    errors,
                    PermissionsField,
                    $"{kind.DisplayName()} に {entry.Permission} という権限はありません。");
            }
        }
    }
}
