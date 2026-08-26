using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// ログイン編集の入力検査。識別子もパスワードも SQL 文へ直接埋め込むので、
/// 形が通ることをサーバーへ送る前にここで見る。
/// </summary>
public static class ServerLoginValidator
{
    /// <summary>SQL Server が受け付けるパスワードの上限。</summary>
    public const int MaxPasswordLength = 128;

    public const string NameField = "name";
    public const string PasswordField = "password";
    public const string ConfirmationField = "confirmation";
    public const string PolicyField = "policy";
    public const string DefaultDatabaseField = "defaultDatabase";
    public const string MappingField = "mapping";

    public static SecurityValidationResult Validate(ServerLoginDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new Dictionary<string, string>();
        ValidateOriginal(errors, draft);
        ValidateName(errors, draft.Name);
        ValidatePassword(errors, draft);
        ValidatePolicy(errors, draft);
        ValidateDefaultDatabase(errors, draft.DefaultDatabase);
        ValidateMappings(errors, draft);

        return errors.Count == 0 ? SecurityValidationResult.Valid : new SecurityValidationResult(errors);
    }

    /// <summary>編集の相手が触ってよいものか。ツリーからは押せないが、経路は 1 本にしておく。</summary>
    private static void ValidateOriginal(IDictionary<string, string> errors, ServerLoginDraft draft)
    {
        if (draft.Original is not { } original)
        {
            return;
        }

        if (original.IsSystem)
        {
            errors[NameField] = "システムのログインは変更できません。";
        }
        else if (!original.Type.IsEditable())
        {
            errors[NameField] = "この種類のログインは編集できません。";
        }
        else if (original.Type.IsWindows()
            && !string.Equals(original.Name.Value, draft.Name.Trim(), StringComparison.Ordinal))
        {
            // Windows のログインは名前が Windows 側の principal の写しなので、
            // サーバーの都合だけで付け替えることはできない。
            errors[NameField] = "Windows 認証のログインの名前は変更できません。";
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
            errors[NameField] = "ログイン名を入力してください。";
        }
        else if (value.Any(char.IsControl))
        {
            errors[NameField] = "ログイン名に制御文字は使えません。";
        }
        else if (value.Length > ServerLoginName.MaxLength)
        {
            errors[NameField] = $"ログイン名は {ServerLoginName.MaxLength} 文字までです。";
        }
    }

    /// <summary>
    /// パスワードは前後の空白も値のうちなので落とさない。
    /// 編集で空のままなら「今のまま」の意味で、そのときは長さも見ない。
    /// </summary>
    private static void ValidatePassword(IDictionary<string, string> errors, ServerLoginDraft draft)
    {
        if (!draft.Type.RequiresPassword())
        {
            return;
        }

        var value = draft.Password;

        if (value.Length == 0)
        {
            if (draft.IsNew)
            {
                errors[PasswordField] = "パスワードを入力してください。";
            }
            else if (draft.MustChangePassword)
            {
                errors[PasswordField] = "次回ログイン時のパスワード変更を求めるには、新しいパスワードが要ります。";
            }
        }
        else if (value.Length > MaxPasswordLength)
        {
            errors[PasswordField] = $"パスワードは {MaxPasswordLength} 文字までです。";
        }

        if (!string.Equals(value, draft.PasswordConfirmation, StringComparison.Ordinal))
        {
            errors[ConfirmationField] = "パスワードが一致しません。";
        }
    }

    /// <summary>
    /// パスワードの規則。サーバーが弾く組み合わせ（期限だけの適用、規則を欠いた MUST_CHANGE）は
    /// どちらもチェックボックスの取り合わせで作れてしまうので、送る前にここで見る。
    /// </summary>
    private static void ValidatePolicy(IDictionary<string, string> errors, ServerLoginDraft draft)
    {
        if (!draft.Type.RequiresPassword())
        {
            return;
        }

        if (draft.EnforceExpiration && !draft.EnforcePolicy)
        {
            errors[PolicyField] = "パスワードの期限を適用するには、パスワード ポリシーの適用が要ります。";
        }
        else if (draft.MustChangePassword && !(draft.EnforcePolicy && draft.EnforceExpiration))
        {
            errors[PolicyField] = "次回ログイン時のパスワード変更を求めるには、パスワード ポリシーと期限の適用が要ります。";
        }
    }

    /// <summary>
    /// ユーザー マッピング。ユーザー名を空のままにした行はログイン名をそのまま使うので、
    /// そのときは名前の形もログイン名で見る（別の欄の理由をここへ二重に出さない）。
    /// </summary>
    private static void ValidateMappings(IDictionary<string, string> errors, ServerLoginDraft draft)
    {
        foreach (var mapping in draft.Mappings.Where(mapping => mapping.IsMapped))
        {
            var user = mapping.UserName.Trim();

            var reason = user.Length > 0
                ? SecurityNameRules.Optional(user, $"{mapping.Database} のユーザー名")
                : SecurityNameRules.Required(draft.Name, "ログイン名");

            SecurityNameRules.Set(errors, MappingField, reason);
            SecurityNameRules.Set(
                errors,
                MappingField,
                SecurityNameRules.Optional(mapping.DefaultSchema, $"{mapping.Database} の既定のスキーマ"));
        }
    }

    private static void ValidateDefaultDatabase(IDictionary<string, string> errors, string database)
    {
        var value = database.Trim();

        // 未指定は許す。サーバーが master を当てる。
        if (value.Length == 0)
        {
            return;
        }

        if (value.Any(char.IsControl))
        {
            errors[DefaultDatabaseField] = "既定のデータベースに制御文字は使えません。";
        }
        else if (value.Length > ServerLoginName.MaxLength)
        {
            errors[DefaultDatabaseField] = $"既定のデータベースは {ServerLoginName.MaxLength} 文字までです。";
        }
    }
}
