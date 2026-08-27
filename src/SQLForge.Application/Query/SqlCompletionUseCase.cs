using SQLForge.Application.Catalog;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Sql;

namespace SQLForge.Application.Query;

/// <summary>
/// エディタのキャレット位置に出す補完の候補を作る。
///
/// どこで何を出すかの判断は <see cref="SqlCompletionAnalyzer"/>（ドメイン）が持ち、
/// ここは決まった種類に応じてカタログを引くだけ。読み込みは <see cref="SchemaCache"/>
/// に任せるので、2 回目からはサーバーへ行かない。
/// </summary>
public sealed class SqlCompletionUseCase(SchemaCache cache)
{
    /// <summary>
    /// 一度に出す候補の上限。テーブルが何千とあるデータベースで一覧を作り切らないための蓋で、
    /// 予約語と関数（あわせて 300 弱）は丸ごと入る大きさにしてある。
    /// </summary>
    public const int MaxItems = 500;

    private static readonly IReadOnlyList<SqlCompletionItem> Words = BuildWords();

    /// <summary>文面とキャレットの位置から候補を作る。出す候補が無ければ空を返す。</summary>
    public async Task<SqlCompletionResult> ExecuteAsync(
        DatabaseName database,
        string sql,
        int caret,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sql);

        var context = SqlCompletionAnalyzer.Analyze(sql, caret);

        if (context.Kind == SqlCompletionKind.None)
        {
            return SqlCompletionResult.Empty;
        }

        var candidates = context.Kind switch
        {
            SqlCompletionKind.Table => await TablesAsync(database, context, cancellationToken).ConfigureAwait(false),
            SqlCompletionKind.Column => await ColumnsAsync(database, context, cancellationToken).ConfigureAwait(false),
            _ => Words
        };

        return new SqlCompletionResult(
            context.ReplaceOffset,
            context.ReplaceLength,
            Narrow(candidates, context.Prefix));
    }

    private async Task<IReadOnlyList<SqlCompletionItem>> TablesAsync(
        DatabaseName database,
        SqlCompletionContext context,
        CancellationToken cancellationToken)
    {
        // dbo. まで打たれているなら、そのスキーマの中だけを出す。
        if (context.Qualifier is { } qualifier)
        {
            return await TablesOfSchemaAsync(database, qualifier, cancellationToken).ConfigureAwait(false);
        }

        var items = new List<SqlCompletionItem>();

        foreach (var schema in await cache.SchemasAsync(database, cancellationToken).ConfigureAwait(false))
        {
            if (!schema.IsSystem)
            {
                items.Add(new SqlCompletionItem(
                    schema.Name.Value,
                    SqlIdentifierText.QuoteIfNeeded(schema.Name.Value),
                    SqlCompletionItemKind.Schema,
                    "スキーマ"));
            }
        }

        foreach (var table in await cache.AllTablesAsync(database, cancellationToken).ConfigureAwait(false))
        {
            items.Add(new SqlCompletionItem(
                table.QualifiedName,
                $"{SqlIdentifierText.QuoteIfNeeded(table.Schema.Value)}.{SqlIdentifierText.QuoteIfNeeded(table.Name)}",
                SqlCompletionItemKind.Table,
                "テーブル"));
        }

        return items;
    }

    private async Task<IReadOnlyList<SqlCompletionItem>> ColumnsAsync(
        DatabaseName database,
        SqlCompletionContext context,
        CancellationToken cancellationToken)
    {
        if (context.Qualifier is { } qualifier)
        {
            var reference = context.Tables.FirstOrDefault(table => table.Matches(qualifier));

            IReadOnlyList<SqlCompletionItem> columns = reference is null
                ? []
                : await ColumnsOfAsync(database, reference, cancellationToken).ConfigureAwait(false);

            // 列が出ないなら、修飾はスキーマ名だったとみて読み替える（dbo. の後ろなど）。
            return columns.Count > 0
                ? columns
                : await TablesOfSchemaAsync(database, qualifier, cancellationToken).ConfigureAwait(false);
        }

        var items = new List<SqlCompletionItem>();

        foreach (var source in context.Tables)
        {
            items.AddRange(await ColumnsOfAsync(database, source, cancellationToken).ConfigureAwait(false));
        }

        // 修飾なしの位置では、列だけでなく予約語も書ける。
        items.AddRange(Words);

        return items;
    }

    private async Task<IReadOnlyList<SqlCompletionItem>> TablesOfSchemaAsync(
        DatabaseName database,
        string schemaName,
        CancellationToken cancellationToken)
    {
        var schemas = await cache.SchemasAsync(database, cancellationToken).ConfigureAwait(false);
        var schema = schemas.FirstOrDefault(candidate =>
            string.Equals(candidate.Name.Value, schemaName, StringComparison.OrdinalIgnoreCase));

        if (schema is null)
        {
            return [];
        }

        var tables = await cache.TablesAsync(database, schema.Name, cancellationToken).ConfigureAwait(false);

        return tables
            .Select(table => new SqlCompletionItem(
                table.Name,
                SqlIdentifierText.QuoteIfNeeded(table.Name),
                SqlCompletionItemKind.Table,
                "テーブル"))
            .ToList();
    }

    private async Task<IReadOnlyList<SqlCompletionItem>> ColumnsOfAsync(
        DatabaseName database,
        SqlTableReference reference,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(database, reference, cancellationToken).ConfigureAwait(false);

        if (resolved is null)
        {
            return [];
        }

        var columns = await cache
            .ColumnsAsync(database, resolved.Schema, resolved.Name, cancellationToken)
            .ConfigureAwait(false);
        var owner = reference.Alias ?? resolved.Name;

        return columns
            .Select(column => new SqlCompletionItem(
                column.Name,
                SqlIdentifierText.QuoteIfNeeded(column.Name),
                SqlCompletionItemKind.Column,
                $"{owner} · {column.DataType}"))
            .ToList();
    }

    /// <summary>文面に書かれたテーブル名を、カタログのテーブルへ突き合わせる。</summary>
    private async Task<TableDescriptor?> ResolveAsync(
        DatabaseName database,
        SqlTableReference reference,
        CancellationToken cancellationToken)
    {
        var tables = await cache.AllTablesAsync(database, cancellationToken).ConfigureAwait(false);

        return tables.FirstOrDefault(table =>
            string.Equals(table.Name, reference.Name, StringComparison.OrdinalIgnoreCase)
            && (reference.Schema is null
                || string.Equals(table.Schema.Value, reference.Schema, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>打ちかけの語で絞り、種類の順（列 → テーブル → … → 予約語）に並べる。</summary>
    private static IReadOnlyList<SqlCompletionItem> Narrow(
        IReadOnlyList<SqlCompletionItem> candidates,
        string prefix) =>
        candidates
            .Where(item => Matches(item, prefix))
            .DistinctBy(item => (item.Kind, item.Label, item.Detail))
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .Take(MaxItems)
            .ToList();

    private static bool Matches(SqlCompletionItem item, string prefix)
    {
        if (prefix.Length == 0 || item.Label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // dbo.orders は orders と打っても出す。
        var dot = item.Label.LastIndexOf('.');

        return dot >= 0 && item.Label.AsSpan(dot + 1).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<SqlCompletionItem> BuildWords()
    {
        var items = new List<SqlCompletionItem>();

        foreach (var keyword in SqlKeywords.Keywords)
        {
            items.Add(new SqlCompletionItem(keyword, keyword, SqlCompletionItemKind.Keyword, "予約語"));
        }

        foreach (var function in SqlKeywords.Functions)
        {
            items.Add(new SqlCompletionItem(function, function, SqlCompletionItemKind.Function, "関数"));
        }

        return items;
    }
}
