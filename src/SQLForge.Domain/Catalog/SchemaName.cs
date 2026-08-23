namespace SQLForge.Domain.Catalog;

/// <summary>
/// スキーマ名。SQL Server の dbo、PostgreSQL の public にあたる。
/// スキーマを持たないエンジンではデータベース名をそのまま入れる。
/// </summary>
public readonly record struct SchemaName
{
    public SchemaName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("スキーマ名は空にできません。", nameof(value));
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("スキーマ名に制御文字は使えません。", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
