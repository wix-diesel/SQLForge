using SQLForge.Domain.Filtering;

namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// ツリーの右クリックから「フィルターの設定」ダイアログへつなぐ口。
/// ツリーだけを組む構成（テストなど）では差さないので、その場合はメニューを出さない。
/// </summary>
public interface IObjectFilterEditor
{
    /// <summary>
    /// フィルターの設定を開く。OK なら新しい絞り込み、キャンセルなら null。
    /// 「フィルターのクリア」を押して OK したときは、条件の無い <see cref="ObjectFilter.None"/> が返る。
    /// </summary>
    /// <param name="path">どの見出しの設定かを示す道のり（例: sales_db/dbo/テーブル）。</param>
    /// <param name="properties">この見出しで条件にできるプロパティ。</param>
    /// <param name="current">今かかっている絞り込み。開いたときの初期値になる。</param>
    Task<ObjectFilter?> EditAsync(
        string path,
        IReadOnlyList<ObjectFilterProperty> properties,
        ObjectFilter current);
}
