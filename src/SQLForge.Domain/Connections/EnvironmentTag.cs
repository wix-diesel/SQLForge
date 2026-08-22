namespace SQLForge.Domain.Connections;

/// <summary>接続先環境の危険度。UI の色分けはこの区分から導出する。</summary>
public enum EnvironmentSeverity
{
    /// <summary>本番。事故が即座に外部へ波及する。</summary>
    Critical,

    /// <summary>ステージング・検証。復旧可能だが共有されている。</summary>
    Elevated,

    /// <summary>開発・ローカル。壊しても本人だけが困る。</summary>
    Normal,

    /// <summary>参照専用のレプリカなど、区分が定まらないもの。</summary>
    Neutral
}

/// <summary>
/// 接続に付ける環境タグ。設計上、接続情報に必ず 1 つ付き、
/// タイトルバーとステータスバーに常時表示される。
/// </summary>
public sealed class EnvironmentTag : IEquatable<EnvironmentTag>
{
    public static readonly EnvironmentTag Production = new("production", "本番", EnvironmentSeverity.Critical, 0);
    public static readonly EnvironmentTag Staging = new("staging", "ステージング", EnvironmentSeverity.Elevated, 1);
    public static readonly EnvironmentTag Local = new("local", "ローカル", EnvironmentSeverity.Normal, 2);
    public static readonly EnvironmentTag Verification = new("verification", "検証", EnvironmentSeverity.Elevated, 3);
    public static readonly EnvironmentTag Unclassified = new("unclassified", "未分類", EnvironmentSeverity.Neutral, 4);

    /// <summary>提示する順序。接続ダイアログの色見本と保存済み接続の見出しの並びに一致する。</summary>
    public static IReadOnlyList<EnvironmentTag> All { get; } =
        new[] { Production, Staging, Local, Verification, Unclassified };

    private EnvironmentTag(string id, string displayName, EnvironmentSeverity severity, int sortOrder)
    {
        Id = id;
        DisplayName = displayName;
        Severity = severity;
        SortOrder = sortOrder;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public EnvironmentSeverity Severity { get; }

    /// <summary>並び順。危険な環境ほど先に出す。</summary>
    public int SortOrder { get; }

    /// <summary>本番接続は既定で読み取り専用にする、というフェーズ 1 の安全機構。</summary>
    public bool RequiresReadOnlyByDefault => Severity == EnvironmentSeverity.Critical;

    public static EnvironmentTag FromId(string id) =>
        All.FirstOrDefault(tag => string.Equals(tag.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentOutOfRangeException(nameof(id), id, "未知の環境タグです。");

    public bool Equals(EnvironmentTag? other) => other is not null && Id == other.Id;

    public override bool Equals(object? obj) => Equals(obj as EnvironmentTag);

    public override int GetHashCode() => Id.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => DisplayName;
}
