using SQLForge.Domain.Catalog;

namespace SQLForge.Domain.Security;

/// <summary>
/// これから作る（あるいは作り替える）ログインのあるべき姿。
/// 検証を通った入力だけがこの形になり、ドライバーはこれを文面に写す。
///
/// 種類とパスワードの組み合わせは常に妥当である前提なので、その一式は作るときにしか決められない
/// （<c>with</c> で後から差し替えられると、コンストラクタの検査をすり抜けてしまう）。
/// </summary>
public sealed record ServerLoginDefinition
{
    /// <param name="name">ログイン名。</param>
    /// <param name="type">ログインの種類（認証方式）。</param>
    /// <param name="password">
    /// 新しいパスワード。SQL Server 認証以外では持たない。
    /// 変更のときに空なら「今のまま」の意味になる。
    /// </param>
    /// <param name="passwordPolicy">パスワードの規則。SQL Server 認証で省いたら既定（どちらも適用）。</param>
    /// <param name="mustChangePassword">次回のログイン時にパスワードの変更を求めるか（MUST_CHANGE）。</param>
    /// <param name="defaultDatabase">既定のデータベース。指定しないなら null（サーバーが master を当てる）。</param>
    public ServerLoginDefinition(
        ServerLoginName name,
        ServerLoginType type,
        string? password = null,
        ServerLoginPasswordPolicy? passwordPolicy = null,
        bool mustChangePassword = false,
        DatabaseName? defaultDatabase = null)
    {
        Name = name;
        Type = type;

        // 種類を切り替えたあとに前の入力が残っていても、パスワードを取らない種類へは持ち出さない
        // （Windows 認証のログインに PASSWORD を付けるとサーバーが弾く）。
        var requiresPassword = type.RequiresPassword();

        Password = requiresPassword && !string.IsNullOrEmpty(password) ? password : null;
        PasswordPolicy = requiresPassword ? passwordPolicy ?? ServerLoginPasswordPolicy.Default : null;
        MustChangePassword = requiresPassword && mustChangePassword;
        DefaultDatabase = defaultDatabase;

        if (!MustChangePassword)
        {
            return;
        }

        // MUST_CHANGE は新しいパスワードと、ポリシー・期限の適用が揃っていないとサーバーが弾く。
        // 黙って落とすと「次回変更を求めたつもり」のログインが出来上がるので、送る前にここで止める。
        if (Password is null)
        {
            throw new ArgumentException(
                "次回ログイン時のパスワード変更を求めるには、新しいパスワードが要ります。",
                nameof(mustChangePassword));
        }

        if (PasswordPolicy is not { EnforcePolicy: true, EnforceExpiration: true })
        {
            throw new ArgumentException(
                "次回ログイン時のパスワード変更を求めるには、パスワード ポリシーと期限の適用が要ります。",
                nameof(mustChangePassword));
        }
    }

    public ServerLoginName Name { get; }

    public ServerLoginType Type { get; }

    /// <summary>新しいパスワード。変えないとき、およびパスワードを取らない種類では null。</summary>
    public string? Password { get; }

    /// <summary>パスワードの規則。パスワードを取らない種類では必ず null。</summary>
    public ServerLoginPasswordPolicy? PasswordPolicy { get; }

    /// <summary>次回のログイン時にパスワードの変更を求めるか。</summary>
    public bool MustChangePassword { get; }

    /// <summary>既定のデータベース。指定しないなら null。</summary>
    public DatabaseName? DefaultDatabase { get; }

    /// <summary>無効にしておくか。種類とは関わらないので後から差し替えてよい。</summary>
    public bool IsDisabled { get; init; }

    /// <summary>所属させるサーバー ロール。種類とは関わらないので後から差し替えてよい。</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// パスワードを抱えているので、レコードが既定で作る「全プロパティの書き出し」を潰す。
    /// 例外のメッセージやログへ平文が紛れ込む経路を、型の側で塞いでおく。
    /// </summary>
    public override string ToString() => $"{nameof(ServerLoginDefinition)} {{ Name = {Name.Value} }}";
}
