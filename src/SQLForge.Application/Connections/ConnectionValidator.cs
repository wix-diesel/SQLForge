using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>入力欄の検証結果。エラーは欄名をキーにして UI 側で赤枠に使う。</summary>
public sealed record ConnectionValidationResult(IReadOnlyDictionary<string, string> Errors)
{
    public static ConnectionValidationResult Valid { get; } =
        new(new Dictionary<string, string>());

    public bool IsValid => Errors.Count == 0;

    public string? FirstError => Errors.Values.FirstOrDefault();

    public string? this[string field] => Errors.TryGetValue(field, out var message) ? message : null;
}

/// <summary>接続情報の妥当性検査。接続処理を持たないこの版でも、入力の整合はここで見る。</summary>
public static class ConnectionValidator
{
    public const string NameField = "name";
    public const string HostField = "host";
    public const string PortField = "port";
    public const string DatabaseField = "database";
    public const string UserField = "user";
    public const string SshHostField = "ssh_host";
    public const string SshPortField = "ssh_port";
    public const string SshUserField = "ssh_user";
    public const string SshKeyField = "ssh_key";
    public const string SshLocalPortField = "ssh_local_port";
    public const string PacketSizeField = "packet_size";
    public const string ConnectTimeoutField = "connect_timeout";
    public const string ExecutionTimeoutField = "execution_timeout";

    public static ConnectionValidationResult Validate(ConnectionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var errors = new Dictionary<string, string>();
        AddIfBlank(errors, NameField, draft.Name, "接続名を入力してください。");
        AddIfBlank(errors, HostField, draft.Host,
            draft.Driver.IsFileBased ? "ファイルパスを入力してください。" : "ホストを入力してください。");
        AddIfBlank(errors, DatabaseField, draft.Database, "データベースを入力してください。");
        ValidatePort(errors, draft);
        ValidateUser(errors, draft);
        ValidateTunnel(errors, draft.Tunnel);
        ValidateAdvanced(errors, draft.Advanced);

        return errors.Count == 0 ? ConnectionValidationResult.Valid : new ConnectionValidationResult(errors);
    }

    /// <summary>
    /// 「SSH トンネル」タブ。切ってあるときは何も見ない
    /// ―― 使わない設定の書きかけで接続できなくならないようにする。
    /// </summary>
    private static void ValidateTunnel(IDictionary<string, string> errors, SshTunnelSettings tunnel)
    {
        if (!tunnel.IsEnabled)
        {
            return;
        }

        AddIfBlank(errors, SshHostField, tunnel.Host, "踏み台のホストを入力してください。");
        AddIfBlank(errors, SshUserField, tunnel.UserName, "踏み台の利用者名を入力してください。");

        if (!ServerAddress.IsValidPort(tunnel.Port))
        {
            errors[SshPortField] = $"ポートは {ServerAddress.MinPort}〜{ServerAddress.MaxPort} で指定してください。";
        }

        if (!tunnel.UsesAutomaticLocalPort && !ServerAddress.IsValidPort(tunnel.LocalPort))
        {
            errors[SshLocalPortField] =
                $"手元のポートは 0（自動）か {ServerAddress.MinPort}〜{ServerAddress.MaxPort} で指定してください。";
        }

        if (tunnel.RequiresPrivateKey && tunnel.PrivateKeyPath.Length == 0)
        {
            errors[SshKeyField] = "秘密鍵のファイルを指定してください。";
        }
    }

    /// <summary>「詳細設定」タブ。範囲は SSMS と同じ。</summary>
    private static void ValidateAdvanced(IDictionary<string, string> errors, AdvancedConnectionSettings advanced)
    {
        if (!AdvancedConnectionSettings.IsValidPacketSize(advanced.PacketSize))
        {
            errors[PacketSizeField] =
                $"パケット サイズは {AdvancedConnectionSettings.MinPacketSize}〜{AdvancedConnectionSettings.MaxPacketSize} バイトで指定してください。";
        }

        if (!AdvancedConnectionSettings.IsValidTimeout(advanced.ConnectTimeoutSeconds))
        {
            errors[ConnectTimeoutField] =
                $"接続タイムアウトは 0〜{AdvancedConnectionSettings.MaxTimeoutSeconds} 秒で指定してください。";
        }

        if (!AdvancedConnectionSettings.IsValidTimeout(advanced.ExecutionTimeoutSeconds))
        {
            errors[ExecutionTimeoutField] =
                $"実行タイムアウトは 0〜{AdvancedConnectionSettings.MaxTimeoutSeconds} 秒で指定してください。";
        }
    }

    private static void ValidatePort(IDictionary<string, string> errors, ConnectionDraft draft)
    {
        if (!draft.Driver.IsFileBased && !ServerAddress.IsValidPort(draft.Port))
        {
            errors[PortField] = $"ポートは {ServerAddress.MinPort}〜{ServerAddress.MaxPort} で指定してください。";
        }
    }

    private static void ValidateUser(IDictionary<string, string> errors, ConnectionDraft draft)
    {
        var requiresUser = !draft.Driver.IsFileBased && draft.Authentication == AuthenticationMethod.Password;
        if (requiresUser && string.IsNullOrWhiteSpace(draft.UserName))
        {
            errors[UserField] = "ユーザーを入力してください。";
        }
    }

    private static void AddIfBlank(IDictionary<string, string> errors, string field, string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[field] = message;
        }
    }
}
