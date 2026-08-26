namespace SQLForge.Application.Security;

/// <summary>
/// スキーマ編集の入力検査。識別子は SQL 文へ直接埋め込むので、
/// 形が通ることをサーバーへ送る前にここで見る。
/// </summary>
public static class SchemaValidator
{
    public const string NameField = "name";
    public const string OwnerField = "owner";

    public static SecurityValidationResult Validate(SchemaDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new Dictionary<string, string>();
        ValidateOriginal(errors, draft);
        SecurityNameRules.Set(errors, NameField, SecurityNameRules.Required(draft.Name, "スキーマ名"));
        SecurityNameRules.Set(errors, OwnerField, SecurityNameRules.Optional(draft.Owner, "所有者"));

        return errors.Count == 0 ? SecurityValidationResult.Valid : new SecurityValidationResult(errors);
    }

    private static void ValidateOriginal(IDictionary<string, string> errors, SchemaDraft draft)
    {
        if (draft.Original is not { } original)
        {
            return;
        }

        if (original.IsSystem)
        {
            errors[NameField] = "システムのスキーマは変更できません。";
        }
        else if (!string.Equals(original.Name.Value, draft.Name.Trim(), StringComparison.Ordinal))
        {
            // SQL Server にスキーマの名前を変える文面は無い（作り直して中身を移すしかない）。
            // SSMS も編集のときは名前欄を触らせないので、ここでも止める。
            errors[NameField] = "スキーマの名前は変更できません。所有者だけを変えられます。";
        }
    }
}
