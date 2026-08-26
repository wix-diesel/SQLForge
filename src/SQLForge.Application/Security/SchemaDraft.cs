using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Security;

/// <summary>
/// スキーマのプロパティ ダイアログで編集中の入力値。
/// エンティティ（<see cref="SchemaDefinition"/>）は常に妥当である前提なので、
/// まだ妥当とは限らない入力はこの器で受け渡す。
/// </summary>
public sealed record SchemaDraft
{
    /// <summary>編集前の姿。新しく作るなら null。</summary>
    public SchemaDescriptor? Original { get; init; }

    public required string Name { get; init; }

    /// <summary>未指定なら空文字。サーバーが作成した利用者を当てる。</summary>
    public required string Owner { get; init; }

    public bool IsNew => Original is null;

    /// <summary>新規作成の初期値。SSMS と同じく所有者は空（実行した利用者）から始める。</summary>
    public static SchemaDraft ForNewSchema() =>
        new()
        {
            Name = string.Empty,
            Owner = string.Empty
        };

    public static SchemaDraft FromDescriptor(SchemaDescriptor schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        return new SchemaDraft
        {
            Original = schema,
            Name = schema.Name.Value,
            Owner = schema.Owner ?? string.Empty
        };
    }

    /// <summary>検証を通ったあとにだけ呼ぶこと。</summary>
    public SchemaDefinition ToDefinition()
    {
        var owner = Owner.Trim();

        return new SchemaDefinition(
            new SchemaName(Name.Trim()),
            owner.Length > 0 ? owner : null);
    }
}
