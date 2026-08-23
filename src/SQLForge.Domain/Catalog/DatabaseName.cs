namespace SQLForge.Domain.Catalog;

/// <summary>
/// データベース名。識別子はパラメータ化できず SQL 文へ直接埋め込むしかないので、
/// 埋め込む前にここで形を保証しておく（引用符付けはドライバー側の責務）。
/// </summary>
public readonly record struct DatabaseName
{
    public DatabaseName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("データベース名は空にできません。", nameof(value));
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("データベース名に制御文字は使えません。", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
