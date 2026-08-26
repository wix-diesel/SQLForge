using SQLForge.Domain.Catalog;
using SQLForge.Domain.Security;

namespace SQLForge.Application.Security;

/// <summary>
/// ログインのプロパティ ダイアログで編集中の入力値。
/// エンティティ（<see cref="ServerLoginDefinition"/>）は常に妥当である前提なので、
/// まだ妥当とは限らない入力はこの器で受け渡す。
/// </summary>
public sealed record ServerLoginDraft
{
    /// <summary>編集前の姿。新しく作るなら null。</summary>
    public ServerLoginDescriptor? Original { get; init; }

    public required string Name { get; init; }

    public required ServerLoginType Type { get; init; }

    /// <summary>
    /// 新しいパスワード。SQL Server 認証以外では使わない。
    /// 編集で空のままなら「今のまま」で、文面にも出さない。
    /// </summary>
    public required string Password { get; init; }

    /// <summary>確認用にもう一度入れたパスワード。打ち間違いを見るためだけに使う。</summary>
    public required string PasswordConfirmation { get; init; }

    /// <summary>パスワード ポリシーを適用するか（CHECK_POLICY）。</summary>
    public bool EnforcePolicy { get; init; }

    /// <summary>パスワードの期限を適用するか（CHECK_EXPIRATION）。</summary>
    public bool EnforceExpiration { get; init; }

    /// <summary>次回のログイン時にパスワードの変更を求めるか（MUST_CHANGE）。</summary>
    public bool MustChangePassword { get; init; }

    /// <summary>未指定なら空文字。サーバーが master を当てる。</summary>
    public required string DefaultDatabase { get; init; }

    /// <summary>無効にしておくか。SSMS の「状態」ページにあたる。</summary>
    public bool IsDisabled { get; init; }

    /// <summary>所属させるサーバー ロール。</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// 開いたときにサーバーから読んだユーザー マッピング。
    /// ログインの一覧には出てこない（データベースごとに読む要りがある）ので、
    /// 一覧の 1 件（<see cref="Original"/>）ではなくここに持たせる。
    /// </summary>
    public IReadOnlyList<LoginUserMapping> OriginalMappings { get; init; } = [];

    /// <summary>ユーザー マッピングの今の姿。データベースごとに 1 行。</summary>
    public IReadOnlyList<LoginUserMappingDraft> Mappings { get; init; } = [];

    public bool IsNew => Original is null;

    /// <summary>
    /// 新規作成の初期値。SSMS と同じく SQL Server 認証から始め、
    /// ポリシー・期限・次回変更のいずれも適用した状態にしておく。
    /// </summary>
    public static ServerLoginDraft ForNewLogin() =>
        new()
        {
            Name = string.Empty,
            Type = ServerLoginType.SqlLogin,
            Password = string.Empty,
            PasswordConfirmation = string.Empty,
            EnforcePolicy = true,
            EnforceExpiration = true,
            MustChangePassword = true,
            DefaultDatabase = string.Empty
        };

    /// <summary>
    /// 既存のログインから写す。パスワードはサーバーから読めない（ハッシュしか無い）ので、
    /// 欄は空から始める。空のまま保存すれば今のパスワードが残る。
    /// </summary>
    public static ServerLoginDraft FromDescriptor(ServerLoginDescriptor login)
    {
        ArgumentNullException.ThrowIfNull(login);

        var policy = login.PasswordPolicy ?? ServerLoginPasswordPolicy.None;

        return new ServerLoginDraft
        {
            Original = login,
            Name = login.Name.Value,
            Type = login.Type,
            Password = string.Empty,
            PasswordConfirmation = string.Empty,
            EnforcePolicy = policy.EnforcePolicy,
            EnforceExpiration = policy.EnforceExpiration,
            MustChangePassword = false,
            DefaultDatabase = login.DefaultDatabase?.Value ?? string.Empty,
            IsDisabled = login.IsDisabled,
            Roles = login.Roles
        };
    }

    /// <summary>
    /// パスワードを抱えているので、レコードが既定で作る「全プロパティの書き出し」を潰す
    /// （<see cref="ServerLoginDefinition.ToString"/> と同じ理由）。下書きは UI と
    /// ユースケースの境目を渡り歩くぶん、定義よりむしろログや例外へ紛れ込みやすい。
    /// </summary>
    public override string ToString() => $"{nameof(ServerLoginDraft)} {{ Name = {Name} }}";

    /// <summary>
    /// マッピングのうち、実際に対応づける行だけを写す。検証を通ったあとにだけ呼ぶこと。
    /// ユーザー名を空のままにした行は、SSMS と同じくログイン名をそのまま使う。
    /// </summary>
    public IReadOnlyList<LoginUserMapping> ToMappings() =>
        Mappings.Where(mapping => mapping.IsMapped).Select(mapping => mapping.ToMapping(Name)).ToList();

    /// <summary>検証を通ったあとにだけ呼ぶこと。</summary>
    public ServerLoginDefinition ToDefinition()
    {
        var database = DefaultDatabase.Trim();

        // 種類を切り替えたあとに前の入力（期限だけ適用など）が残っていても、
        // パスワードを取らない種類では規則そのものを持たせない。
        ServerLoginPasswordPolicy? policy = Type.RequiresPassword()
            ? new ServerLoginPasswordPolicy(EnforcePolicy, EnforceExpiration)
            : null;

        return new ServerLoginDefinition(
            new ServerLoginName(Name.Trim()),
            Type,
            // パスワードは前後の空白も値のうちなので、名前と違って落とさない。
            Password,
            policy,
            MustChangePassword,
            database.Length > 0 ? new DatabaseName(database) : null)
        {
            IsDisabled = IsDisabled,
            Roles = Roles
        };
    }
}
