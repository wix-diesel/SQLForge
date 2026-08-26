using CommunityToolkit.Mvvm.Input;
using SQLForge.Domain.Catalog;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// 「スキーマ」の見出し。SSMS はスキーマを [セキュリティ] の下に置くが、
/// このツリーはテーブルをスキーマの下にぶら下げるので、同じ枝を 2 か所に出さず
/// データベースの直下 1 か所にまとめている。所有者の付け替えはここから行う。
/// </summary>
public sealed partial class SchemasNode : ObjectExplorerNode
{
    private readonly CatalogContext _context;
    private readonly DatabaseName _database;

    public SchemasNode(CatalogContext context, DatabaseName database)
        : base("スキーマ", canExpand: true)
    {
        _context = context;
        _database = database;
    }

    /// <summary>編集の行き先がつながっている構成か。押せるのに何も起きないメニューは出さない。</summary>
    public bool CanEdit => _context.Security?.SchemaEditor is not null;

    /// <summary>右クリックの「新しいスキーマ」。作られたら一覧を読み直す。</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task NewSchemaAsync(CancellationToken cancellationToken)
    {
        if (_context.Security?.SchemaEditor is not { } editor)
        {
            return;
        }

        if (await editor.CreateAsync(_context.Session, _database).ConfigureAwait(true))
        {
            await ReloadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>右クリックの「最新の情報に更新」。</summary>
    [RelayCommand]
    private Task RefreshAsync(CancellationToken cancellationToken) => ReloadAsync(cancellationToken);

    protected override async Task<IReadOnlyList<ObjectExplorerNode>> LoadChildrenAsync(
        CancellationToken cancellationToken)
    {
        var schemas = await _context.Schemas
            .ExecuteAsync(_context.Session, _database, cancellationToken)
            .ConfigureAwait(true);

        return schemas.Select(schema => new SchemaNode(_context, _database, schema, this)).ToList();
    }

    /// <summary>読み終えたら件数を見出しの右に出す（ほかの見出しと同じ）。</summary>
    protected override void OnChildrenLoaded(IReadOnlyList<ObjectExplorerNode> children) =>
        Detail = children.Count.ToString();

    protected override void OnChildrenFailed() => Detail = null;
}
