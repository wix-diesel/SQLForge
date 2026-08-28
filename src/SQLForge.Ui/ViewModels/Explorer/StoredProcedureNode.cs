using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Catalog;
using SQLForge.Domain.Filtering;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// ストアド プロシージャ 1 件。ツリーの下にパラメーターの見出しを持つ。
/// 右クリックから実行、または定義の表示ができる。
/// </summary>
public sealed partial class StoredProcedureNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseName _database;
    private readonly StoredProcedureDescriptor _descriptor;

    public StoredProcedureNode(CatalogContext context, DatabaseName database, StoredProcedureDescriptor descriptor)
        : base(descriptor.Name, canExpand: true)
    {
        _context = context;
        _database = database;
        _descriptor = descriptor;
        Detail = descriptor.ParameterCount > 0 ? $"パラメーター {descriptor.ParameterCount}" : null;
    }

    public string QualifiedName => _descriptor.QualifiedName;

    /// <summary>絞り込みに掛ける値。名前に加えて、作成日でも絞り込めるようにする。</summary>
    public override ObjectFilterTarget FilterTarget => new(Title, _descriptor.CreatedAt);

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(
        [
            new CatalogFolderNode("パラメーター", LoadParametersAsync)
        ]);

    private async Task<IReadOnlyList<ObjectExplorerNode>> LoadParametersAsync(CancellationToken cancellationToken)
    {
        var parameters = await _context.StoredProcedureParameters
            .ExecuteAsync(_context.Session, _database, _descriptor.Schema, _descriptor.Name, cancellationToken)
            .ConfigureAwait(true);

        return parameters.Select(parameter => new StoredProcedureParameterNode(parameter)).ToList();
    }

    /// <summary>作業領域がつながっている構成か（<see cref="TableNode.CanQuery"/> と同じ理由）。</summary>
    public bool CanQuery => _context.Query is not null;

    /// <summary>右クリックの「実行」。パラメーターが要るものはこのまま編集してから走らせる。</summary>
    [RelayCommand(CanExecute = nameof(CanQuery))]
    private void Execute() => _context.Query?.OpenAndRunQuery(_database, $"EXEC {QuotedQualifiedName};");

    /// <summary>右クリックの「定義を表示」。sys.sql_modules の定義文を選択して結果グリッドに出す。</summary>
    [RelayCommand(CanExecute = nameof(CanQuery))]
    private void ViewDefinition() =>
        _context.Query?.OpenAndRunQuery(
            _database,
            $"SELECT OBJECT_DEFINITION(OBJECT_ID(N'{EscapeLiteral(QuotedQualifiedName)}')) AS definition;");

    private string QuotedQualifiedName =>
        $"{Quote(_descriptor.Schema.Value)}.{Quote(_descriptor.Name)}";

    private static string Quote(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    /// <summary>
    /// 文字列リテラルに埋め込む前のエスケープ。角括弧で囲んだ識別子でも、
    /// スキーマ名やプロシージャ名自体に ' を含む区切り識別子は作れるので、
    /// 角括弧の引用符付けとは別にここでも二重化しておく。
    /// </summary>
    private static string EscapeLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
