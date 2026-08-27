namespace SQLForge.Domain.Connections;

/// <summary>
/// 保存済みの接続 1 件。接続ダイアログが編集し、一覧に並べる単位。
/// 実際の接続は担わない（フェーズ 1 のこの版では接続処理そのものが未実装）。
/// </summary>
public sealed class ConnectionProfile
{
    public ConnectionProfile(
        ConnectionProfileId id,
        string name,
        EnvironmentTag environment,
        ConnectionTarget target,
        ConnectionCredentials credentials,
        AccessMode accessMode,
        SshTunnelSettings? tunnel = null,
        AdvancedConnectionSettings? advanced = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("接続名は空にできません。", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        AccessMode = accessMode;
        Tunnel = tunnel ?? SshTunnelSettings.Disabled;
        Advanced = advanced ?? AdvancedConnectionSettings.Default;
    }

    public ConnectionProfileId Id { get; }

    public string Name { get; }

    public EnvironmentTag Environment { get; }

    public ConnectionTarget Target { get; }

    public ConnectionCredentials Credentials { get; }

    public AccessMode AccessMode { get; }

    /// <summary>SSH の踏み台ごしに繋ぐかどうか（「SSH トンネル」タブ）。</summary>
    public SshTunnelSettings Tunnel { get; }

    /// <summary>接続の細かい指定（「詳細設定」タブ）。</summary>
    public AdvancedConnectionSettings Advanced { get; }

    public bool IsReadOnly => AccessMode == AccessMode.ReadOnly;

    /// <summary>本番接続を書き込み可で開こうとしている、という警告すべき状態。</summary>
    public bool IsUnsafeWriteAccess =>
        Environment.RequiresReadOnlyByDefault && AccessMode == AccessMode.ReadWrite;

    /// <summary>環境タグから導かれる既定のアクセス種別。新規作成と環境タグ変更時に使う。</summary>
    public static AccessMode DefaultAccessModeFor(EnvironmentTag environment) =>
        environment.RequiresReadOnlyByDefault ? AccessMode.ReadOnly : AccessMode.ReadWrite;

    /// <summary>まだ何も入力されていない新規接続。ダイアログの「新しい接続」に対応する。</summary>
    public static ConnectionProfile CreateDraft(DatabaseDriver driver)
    {
        var environment = EnvironmentTag.Local;
        return new ConnectionProfile(
            ConnectionProfileId.New(),
            "新しい接続",
            environment,
            new ConnectionTarget(driver, new ServerAddress("localhost", driver.DefaultPort), driver.DefaultDatabase),
            new ConnectionCredentials(string.Empty, AuthenticationMethod.Password, true),
            DefaultAccessModeFor(environment));
    }

    /// <summary>指定の検索語に一致するか。保存済み接続の絞り込みに使う。</summary>
    public bool Matches(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return true;
        }

        var needle = term.Trim();
        return Contains(Name, needle)
            || Contains(Target.Address.Host, needle)
            || Contains(Target.Database, needle)
            || Contains(Target.Driver.DisplayName, needle)
            || Contains(Credentials.UserName, needle);
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
