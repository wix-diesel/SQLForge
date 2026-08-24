using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>入力欄の検証結果。エラーは欄名をキーにして UI 側で赤枠に使う。</summary>
public sealed record DatabaseUserValidationResult(IReadOnlyDictionary<string, string> Errors)
{
    public static DatabaseUserValidationResult Valid { get; } = new(new Dictionary<string, string>());

    public bool IsValid => Errors.Count == 0;

    public string? FirstError => Errors.Values.FirstOrDefault();

    public string? this[string field] => Errors.TryGetValue(field, out var message) ? message : null;
}

/// <summary>
/// ユーザー編集の入力検査。識別子は SQL 文へ直接埋め込むので、
/// 形が通ることをサーバーへ送る前にここで見る。
/// </summary>
public static class DatabaseUserValidator
{
    public const string NameField = "name";
    public const string LoginField = "login";
    public const string DefaultSchemaField = "defaultSchema";

    public static DatabaseUserValidationResult Validate(DatabaseUserDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new Dictionary<string, string>();
        ValidateOriginal(errors, draft.Original);
        ValidateName(errors, draft.Name);
        ValidateLogin(errors, draft);
        ValidateDefaultSchema(errors, draft.DefaultSchema);

        return errors.Count == 0 ? DatabaseUserValidationResult.Valid : new DatabaseUserValidationResult(errors);
    }

    /// <summary>編集の相手が触ってよいものか。ツリーからは押せないが、経路は 1 本にしておく。</summary>
    private static void ValidateOriginal(IDictionary<string, string> errors, DatabaseUserDescriptor? original)
    {
        if (original is null)
        {
            return;
        }

        if (original.IsSystem)
        {
            errors[NameField] = "システムのユーザーは変更できません。";
        }
        else if (!original.Type.IsEditable())
        {
            errors[NameField] = "この種類のユーザーは編集できません。";
        }
    }

    private static void ValidateName(IDictionary<string, string> errors, string name)
    {
        if (errors.ContainsKey(NameField))
        {
            return;
        }

        var value = name.Trim();

        if (value.Length == 0)
        {
            errors[NameField] = "ユーザー名を入力してください。";
        }
        else if (value.Any(char.IsControl))
        {
            errors[NameField] = "ユーザー名に制御文字は使えません。";
        }
        else if (value.Length > DatabaseUserName.MaxLength)
        {
            errors[NameField] = $"ユーザー名は {DatabaseUserName.MaxLength} 文字までです。";
        }
    }

    private static void ValidateLogin(IDictionary<string, string> errors, DatabaseUserDraft draft)
    {
        if (!draft.Type.RequiresLogin())
        {
            return;
        }

        var value = draft.LoginName.Trim();

        if (value.Length == 0)
        {
            errors[LoginField] = "ログイン名を入力してください。";
        }
        else if (value.Any(char.IsControl))
        {
            errors[LoginField] = "ログイン名に制御文字は使えません。";
        }
        else if (value.Length > DatabaseUserName.MaxLength)
        {
            errors[LoginField] = $"ログイン名は {DatabaseUserName.MaxLength} 文字までです。";
        }
    }

    private static void ValidateDefaultSchema(IDictionary<string, string> errors, string schema)
    {
        var value = schema.Trim();

        // 未指定は許す。サーバーが dbo を当てる。
        if (value.Length == 0)
        {
            return;
        }

        if (value.Any(char.IsControl))
        {
            errors[DefaultSchemaField] = "既定のスキーマに制御文字は使えません。";
        }
        else if (value.Length > DatabaseUserName.MaxLength)
        {
            errors[DefaultSchemaField] = $"既定のスキーマは {DatabaseUserName.MaxLength} 文字までです。";
        }
    }
}
