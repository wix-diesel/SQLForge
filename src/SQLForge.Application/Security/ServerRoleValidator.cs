namespace SQLForge.Application.Security;

/// <summary>
/// サーバー ロール編集の入力検査。識別子は SQL 文へ直接埋め込むので、
/// 形が通ることをサーバーへ送る前にここで見る。
/// </summary>
public static class ServerRoleValidator
{
    public const string NameField = "name";
    public const string OwnerField = "owner";
    public const string MembershipField = "membership";

    public static SecurityValidationResult Validate(ServerRoleDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new Dictionary<string, string>();
        ValidateFixedRole(errors, draft);
        SecurityNameRules.Set(errors, NameField, SecurityNameRules.Required(draft.Name, "ロール名"));
        SecurityNameRules.Set(errors, OwnerField, SecurityNameRules.Optional(draft.Owner, "所有者"));

        return errors.Count == 0 ? SecurityValidationResult.Valid : new SecurityValidationResult(errors);
    }

    /// <summary>
    /// 固定サーバー ロール（sysadmin など）は作り替えられない。
    /// メンバーの出し入れは日常の操作なので、そこだけは通す。
    /// </summary>
    private static void ValidateFixedRole(IDictionary<string, string> errors, ServerRoleDraft draft)
    {
        if (draft.Original is not { IsFixedRole: true } original)
        {
            return;
        }

        if (!string.Equals(original.Name.Value, draft.Name.Trim(), StringComparison.Ordinal))
        {
            errors[NameField] = "固定のサーバー ロールは名前を変更できません。";
        }

        if (!string.Equals(original.Owner ?? string.Empty, draft.Owner.Trim(), StringComparison.Ordinal))
        {
            errors[OwnerField] = "固定のサーバー ロールは所有者を変更できません。";
        }

        // 固定ロールを別のロールへ入れることはできない（sysadmin を何かに入れる、など）。
        if (!SecurityNameRules.SameSet(original.Memberships, draft.Memberships))
        {
            errors[MembershipField] = "固定のサーバー ロールのメンバーシップは変更できません。";
        }
    }
}
