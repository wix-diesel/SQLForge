using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// データベース ロール編集の入力検査。識別子は SQL 文へ直接埋め込むので、
/// 形が通ることをサーバーへ送る前にここで見る。
/// </summary>
public static class DatabaseRoleValidator
{
    public const string NameField = "name";
    public const string OwnerField = "owner";
    public const string SchemasField = "schemas";

    public static SecurityValidationResult Validate(DatabaseRoleDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new Dictionary<string, string>();
        ValidateFixedRole(errors, draft);
        SecurityNameRules.Set(errors, NameField, SecurityNameRules.Required(draft.Name, "ロール名"));
        SecurityNameRules.Set(errors, OwnerField, SecurityNameRules.Optional(draft.Owner, "所有者"));
        ValidateSchemas(errors, draft.OwnedSchemas);

        return errors.Count == 0 ? SecurityValidationResult.Valid : new SecurityValidationResult(errors);
    }

    /// <summary>
    /// 固定データベース ロール（db_owner など）は作り替えられない。
    /// メンバーの出し入れは日常の操作なので、そこだけは通す。
    /// </summary>
    private static void ValidateFixedRole(IDictionary<string, string> errors, DatabaseRoleDraft draft)
    {
        if (draft.Original is not { IsFixedRole: true } original)
        {
            return;
        }

        if (!string.Equals(original.Name.Value, draft.Name.Trim(), StringComparison.Ordinal))
        {
            errors[NameField] = "固定のデータベース ロールは名前を変更できません。";
        }

        if (!string.Equals(original.Owner ?? string.Empty, draft.Owner.Trim(), StringComparison.Ordinal))
        {
            errors[OwnerField] = "固定のデータベース ロールは所有者を変更できません。";
        }

        if (!SecurityNameRules.SameSet(original.OwnedSchemas, draft.OwnedSchemas))
        {
            errors[SchemasField] = "固定のデータベース ロールが所有するスキーマは変更できません。";
        }
    }

    private static void ValidateSchemas(IDictionary<string, string> errors, IReadOnlyList<string> schemas)
    {
        foreach (var schema in schemas)
        {
            SecurityNameRules.Set(errors, SchemasField, SecurityNameRules.Required(schema, "スキーマ名"));
        }
    }
}
