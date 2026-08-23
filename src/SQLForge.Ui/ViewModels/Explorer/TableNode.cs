using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Catalog;
using SQLForge.Ui.Presentation;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>テーブル 1 件。ツリーの葉。右クリックからクエリエディタを開ける。</summary>
public sealed partial class TableNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseName _database;
    private readonly TableDescriptor _descriptor;

    public TableNode(CatalogContext context, DatabaseName database, TableDescriptor descriptor)
        : base(descriptor.Name, canExpand: true)
    {
        _context = context;
        _database = database;
        _descriptor = descriptor;
        Detail = RowCountFormat.Describe(descriptor.RowCount);
    }

    public string QualifiedName => _descriptor.QualifiedName;

    public long? RowCount => _descriptor.RowCount;

    protected override Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectExplorerNode>>(
        [
            new CatalogFolderNode("列", LoadColumnsAsync)
        ]);

    private async Task<IReadOnlyList<ObjectExplorerNode>> LoadColumnsAsync(CancellationToken cancellationToken)
    {
        var columns = await _context.Columns
            .ExecuteAsync(_context.Session, _database, _descriptor.Schema, _descriptor.Name, cancellationToken)
            .ConfigureAwait(true);

        return columns.Select(column => new ColumnNode(column)).ToList();
    }

    /// <summary>
    /// 作業領域がつながっている構成か。ツリーだけを組むとき（テストなど）は
    /// 行き先が無いので、押せるのに何も起きないメニューを出さない。
    /// </summary>
    public bool CanQuery => _context.Query is not null;

    /// <summary>
    /// 右クリックの「クエリを実行」。このテーブルのあるデータベースを実行先にして、
    /// 空のエディタを開く。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanQuery))]
    private void OpenQuery() => _context.Query?.OpenNewQuery(_database);

    /// <summary>
    /// 右クリックの「先頭 1000 件を表示」。このテーブルの先頭 1000 行を選ぶ文面を
    /// 組み立てて、エディタを開くと同時に実行する。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanQuery))]
    private void ShowTopRows() =>
        _context.Query?.OpenAndRunQuery(_database, $"SELECT TOP (1000) * FROM {QuotedQualifiedName};");

    /// <summary>
    /// スキーマ名・テーブル名それぞれを角括弧で囲む。閉じ括弧はテーブル名の側に
    /// 含まれ得るので、二重にして閉じられないようにする（SQL Server の識別子の作法）。
    /// </summary>
    private string QuotedQualifiedName =>
        $"{Quote(_descriptor.Schema.Value)}.{Quote(_descriptor.Name)}";

    private static string Quote(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
