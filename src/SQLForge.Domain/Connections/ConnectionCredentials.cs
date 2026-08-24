namespace SQLForge.Domain.Connections;

/// <summary>「誰として繋ぐか」。パスワードそのものは保持しない。</summary>
public sealed record ConnectionCredentials
{
    /// <summary>OS 統合認証など、ユーザー名を入力しない接続。</summary>
    public static ConnectionCredentials Anonymous { get; } = new(string.Empty, AuthenticationMethod.Integrated, false);

    public ConnectionCredentials(string userName, AuthenticationMethod method, bool storeSecretInKeyring)
    {
        Method = method;

        // OS 統合認証では名乗る相手を OS が決める。ドライバーは利用者名もパスワードも見ないので、
        // パスワード認証から切り替えたときに入力欄へ残っていた値をここで落とす。
        // 残したままだと、保存内容と接続 URL が「使われない利用者名で繋ぐ」ように見えてしまう。
        var usesOsIdentity = method == AuthenticationMethod.Integrated;

        UserName = usesOsIdentity ? string.Empty : userName?.Trim() ?? string.Empty;
        StoreSecretInKeyring = !usesOsIdentity && storeSecretInKeyring;
    }

    public string UserName { get; }

    public AuthenticationMethod Method { get; }

    /// <summary>資格情報を OS のキーリング（Linux では Secret Service）に預けるか。</summary>
    public bool StoreSecretInKeyring { get; }

    /// <summary>パスワード入力欄を出すかどうか。</summary>
    public bool RequiresSecret => Method == AuthenticationMethod.Password;

    /// <summary>OS の資格情報で名乗る接続か（SQL Server の Windows 認証・Kerberos）。</summary>
    public bool UsesOsIdentity => Method == AuthenticationMethod.Integrated;
}
