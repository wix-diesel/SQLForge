namespace SQLForge.Application.Security;

/// <summary>
/// セキュリティ関係の入力欄の検証結果。ユーザーとログインで欄の名前は違うが、
/// 「欄ごとの理由を持ち、1 つでもあれば妥当ではない」という形は同じなので器を共有する。
/// エラーは欄名をキーにして UI 側で赤枠に使う。
/// </summary>
public sealed record SecurityValidationResult(IReadOnlyDictionary<string, string> Errors)
{
    public static SecurityValidationResult Valid { get; } = new(new Dictionary<string, string>());

    public bool IsValid => Errors.Count == 0;

    public string? FirstError => Errors.Values.FirstOrDefault();

    public string? this[string field] => Errors.TryGetValue(field, out var message) ? message : null;
}
