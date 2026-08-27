using SQLForge.Application.Abstractions;
using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Catalog;

/// <summary>
/// 補完のために読んだカタログを覚えておく。開いている接続 1 本につき 1 つ持つ。
///
/// エディタは 1 文字打つたびに候補を求めるので、そのつどサーバーへ問い合わせるわけには
/// いかない。ツリーの遅延読み込みと同じで、要求されたところだけを読んで貯める。
/// </summary>
public sealed class SchemaCache(
    IDatabaseSession session,
    ListSchemasUseCase schemas,
    ListTablesUseCase tables,
    ListColumnsUseCase columns)
{
    private readonly Dictionary<string, IReadOnlyList<SchemaDescriptor>> _schemas = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<TableDescriptor>> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<ColumnDescriptor>> _columns = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>データベース内のスキーマ一覧。</summary>
    public async Task<IReadOnlyList<SchemaDescriptor>> SchemasAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default)
    {
        var key = database.Value;

        if (_schemas.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = await schemas.ExecuteAsync(session, database, cancellationToken).ConfigureAwait(false);
        _schemas[key] = loaded;

        return loaded;
    }

    /// <summary>スキーマ内のテーブル一覧。</summary>
    public async Task<IReadOnlyList<TableDescriptor>> TablesAsync(
        DatabaseName database,
        SchemaName schema,
        CancellationToken cancellationToken = default)
    {
        var key = $"{database.Value}.{schema.Value}";

        if (_tables.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = await tables.ExecuteAsync(session, database, schema, cancellationToken).ConfigureAwait(false);
        _tables[key] = loaded;

        return loaded;
    }

    /// <summary>
    /// ユーザーが作ったスキーマすべてのテーブル。修飾なしで書かれたテーブル名を
    /// 解くのに使う（sys や INFORMATION_SCHEMA は候補に出さない）。
    /// </summary>
    public async Task<IReadOnlyList<TableDescriptor>> AllTablesAsync(
        DatabaseName database,
        CancellationToken cancellationToken = default)
    {
        var found = new List<TableDescriptor>();

        foreach (var schema in await SchemasAsync(database, cancellationToken).ConfigureAwait(false))
        {
            if (schema.IsSystem)
            {
                continue;
            }

            found.AddRange(await TablesAsync(database, schema.Name, cancellationToken).ConfigureAwait(false));
        }

        return found;
    }

    /// <summary>テーブルの列一覧。</summary>
    public async Task<IReadOnlyList<ColumnDescriptor>> ColumnsAsync(
        DatabaseName database,
        SchemaName schema,
        string table,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var key = $"{database.Value}.{schema.Value}.{table}";

        if (_columns.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = await columns.ExecuteAsync(session, database, schema, table, cancellationToken).ConfigureAwait(false);
        _columns[key] = loaded;

        return loaded;
    }

    /// <summary>貯めたものを捨てる。テーブルを作り替えたあとに読み直させる。</summary>
    public void Forget()
    {
        _schemas.Clear();
        _tables.Clear();
        _columns.Clear();
    }
}
