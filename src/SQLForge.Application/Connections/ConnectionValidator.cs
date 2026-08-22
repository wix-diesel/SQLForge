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

        return errors.Count == 0 ? ConnectionValidationResult.Valid : new ConnectionValidationResult(errors);
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
