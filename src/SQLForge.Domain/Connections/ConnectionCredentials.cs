namespace SQLForge.Domain.Connections;

/// <summary>「誰として繋ぐか」。パスワードそのものは保持しない。</summary>
public sealed record ConnectionCredentials
{
    /// <summary>OS 統合認証など、ユーザー名を入力しない接続。</summary>
    public static ConnectionCredentials Anonymous { get; } = new(string.Empty, AuthenticationMethod.Integrated, false);

    public ConnectionCredentials(string userName, AuthenticationMethod method, bool storeSecretInKeyring)
    {
        UserName = userName?.Trim() ?? string.Empty;
        Method = method;
        StoreSecretInKeyring = storeSecretInKeyring;
    }

    public string UserName { get; }

    public AuthenticationMethod Method { get; }

    /// <summary>資格情報を OS のキーリング（Linux では Secret Service）に預けるか。</summary>
    public bool StoreSecretInKeyring { get; }

    /// <summary>パスワード入力欄を出すかどうか。</summary>
    public bool RequiresSecret => Method == AuthenticationMethod.Password;
}
