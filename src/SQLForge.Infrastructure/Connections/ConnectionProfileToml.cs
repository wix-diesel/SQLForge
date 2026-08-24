using System.Globalization;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 保存済み接続 1 件と TOML のテーブル 1 つの対応。
/// 書き出すのは「どこへ誰として繋ぐか」までで、パスワードは含めない
/// （資格情報は <see cref="Application.Abstractions.ISecretStore"/> の担当）。
/// </summary>
internal static class ConnectionProfileToml
{
    private const string TableName = "connection";

    private const string Header =
        """
        # SQLForge の保存済み接続。
        # パスワードはここには書かない（OS のキーリングに預ける）。
        # 手で編集してもよいが、アプリからの保存で書き直される。


        """;

    public static string Write(IEnumerable<ConnectionProfile> profiles) =>
        TomlArrayOfTables.Write(TableName, profiles.Select(ToTable), Header);

    public static IReadOnlyList<ConnectionProfile> Read(string text) =>
        TomlArrayOfTables.Read(TableName, text).Select(ToProfile).ToList();

    private static IReadOnlyList<KeyValuePair<string, object>> ToTable(ConnectionProfile profile) =>
    [
        new("id", profile.Id.ToString()),
        new("name", profile.Name),
        new("environment", profile.Environment.Id),
        new("driver", profile.Target.Driver.Id),
        new("host", profile.Target.Address.Host),
        new("port", profile.Target.Address.Port),
        new("database", profile.Target.Database),
        new("user", profile.Credentials.UserName),
        new("authentication", Authentications.NameOf(profile.Credentials.Method)),
        new("store_secret_in_keyring", profile.Credentials.StoreSecretInKeyring),
        new("tls", TlsModes.NameOf(profile.Target.Tls)),
        new("access_mode", AccessModes.NameOf(profile.AccessMode))
    ];

    private static ConnectionProfile ToProfile(IReadOnlyDictionary<string, string> table)
    {
        var driver = Lookup(table, "driver", DatabaseDriver.FromId);
        var address = new ServerAddress(Text(table, "host"), Lookup(table, "port", ParsePort));

        return new ConnectionProfile(
            Lookup(table, "id", ConnectionProfileId.Parse),
            Text(table, "name"),
            Lookup(table, "environment", EnvironmentTag.FromId),
            new ConnectionTarget(driver, address, Text(table, "database"), Lookup(table, "tls", TlsModes.FromName)),
            new ConnectionCredentials(
                Text(table, "user"),
                Lookup(table, "authentication", Authentications.FromName),
                Lookup(table, "store_secret_in_keyring", ParseBool)),
            Lookup(table, "access_mode", AccessModes.FromName));
    }

    private static string Text(IReadOnlyDictionary<string, string> table, string key) =>
        table.TryGetValue(key, out var value) ? value : throw Missing(key);

    /// <summary>値の読み替えで失敗したときに、どのキーが悪いのかまで含めて伝える。</summary>
    private static T Lookup<T>(IReadOnlyDictionary<string, string> table, string key, Func<string, T> convert)
    {
        var value = Text(table, key);

        try
        {
            return convert(value);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            throw new FormatException($"{key} の値を読めません: {value}", exception);
        }
    }

    private static int ParsePort(string value) =>
        int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static bool ParseBool(string value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => throw new FormatException($"true か false で書いてください: {value}")
    };

    private static FormatException Missing(string key) => new($"{key} がありません。");

    private static class Authentications
    {
        public static string NameOf(AuthenticationMethod method) => method switch
        {
            AuthenticationMethod.Password => "password",
            AuthenticationMethod.Integrated => "integrated",
            AuthenticationMethod.Certificate => "certificate",
            _ => throw new NotSupportedException($"知らない認証方式です: {method}")
        };

        public static AuthenticationMethod FromName(string name) => name switch
        {
            "password" => AuthenticationMethod.Password,
            "integrated" => AuthenticationMethod.Integrated,
            "certificate" => AuthenticationMethod.Certificate,
            _ => throw new FormatException($"知らない認証方式です: {name}")
        };
    }

    private static class TlsModes
    {
        public static string NameOf(TlsMode tls) => tls switch
        {
            TlsMode.Disabled => "disabled",
            TlsMode.Prefer => "prefer",
            TlsMode.Require => "require",
            TlsMode.VerifyFull => "verify_full",
            _ => throw new NotSupportedException($"知らない TLS の要求レベルです: {tls}")
        };

        public static TlsMode FromName(string name) => name switch
        {
            "disabled" => TlsMode.Disabled,
            "prefer" => TlsMode.Prefer,
            "require" => TlsMode.Require,
            "verify_full" => TlsMode.VerifyFull,
            _ => throw new FormatException($"知らない TLS の要求レベルです: {name}")
        };
    }

    private static class AccessModes
    {
        public static string NameOf(AccessMode mode) => mode switch
        {
            AccessMode.ReadOnly => "read_only",
            AccessMode.ReadWrite => "read_write",
            _ => throw new NotSupportedException($"知らないアクセス種別です: {mode}")
        };

        public static AccessMode FromName(string name) => name switch
        {
            "read_only" => AccessMode.ReadOnly,
            "read_write" => AccessMode.ReadWrite,
            _ => throw new FormatException($"知らないアクセス種別です: {name}")
        };
    }
}
