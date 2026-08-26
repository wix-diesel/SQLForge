namespace SQLForge.Domain.Security;

/// <summary>
/// SQL Server 認証のログインに掛けるパスワードの規則。SSMS の「パスワード ポリシーを適用する」
/// 「パスワードの期限を適用する」の 2 つに対応し、CHECK_POLICY / CHECK_EXPIRATION として文面へ出る。
///
/// 期限だけを適用することはできない（サーバーが弾く）ので、その組み合わせはここで止める。
/// 後から差し替えられると検査をすり抜けるため、値は作るときにしか決められない。
/// </summary>
public readonly record struct ServerLoginPasswordPolicy
{
    /// <param name="enforcePolicy">Windows のパスワード ポリシーを適用するか（CHECK_POLICY）。</param>
    /// <param name="enforceExpiration">パスワードの有効期限を適用するか（CHECK_EXPIRATION）。</param>
    public ServerLoginPasswordPolicy(bool enforcePolicy, bool enforceExpiration)
    {
        if (enforceExpiration && !enforcePolicy)
        {
            throw new ArgumentException(
                "パスワードの期限を適用するには、パスワード ポリシーの適用が要ります。",
                nameof(enforceExpiration));
        }

        EnforcePolicy = enforcePolicy;
        EnforceExpiration = enforceExpiration;
    }

    /// <summary>SSMS の新規作成と同じ既定。ポリシーも期限も適用する。</summary>
    public static ServerLoginPasswordPolicy Default { get; } = new(enforcePolicy: true, enforceExpiration: true);

    /// <summary>どちらも適用しない。</summary>
    public static ServerLoginPasswordPolicy None { get; } = new(enforcePolicy: false, enforceExpiration: false);

    public bool EnforcePolicy { get; }

    public bool EnforceExpiration { get; }
}
