using System.Globalization;

namespace SQLForge.Ui.ViewModels.Workspace;

/// <summary>
/// 結果ペインのタブ 1 枚。「結果 1」「結果 2」…と「メッセージ」を同じ型で並べる
/// （design/Main.dc.html のタブ帯と同じ並び）。
/// </summary>
public sealed class QueryTabViewModel
{
    private QueryTabViewModel(string title, string? badge, QueryResultSetViewModel? resultSet, string text)
    {
        Title = title;
        Badge = badge;
        ResultSet = resultSet;
        Text = text;
    }

    public string Title { get; }

    /// <summary>見出しの右に出す件数。メッセージのタブでは null。</summary>
    public string? Badge { get; }

    public bool HasBadge => Badge is not null;

    /// <summary>グリッドのタブならその中身。メッセージのタブでは null。</summary>
    public QueryResultSetViewModel? ResultSet { get; }

    /// <summary>メッセージのタブの本文。</summary>
    public string Text { get; }

    /// <summary>グリッドを出すタブか。false ならメッセージを出す。</summary>
    public bool IsGrid => ResultSet is not null;

    public static QueryTabViewModel ForResult(int number, QueryResultSetViewModel resultSet) =>
        new(
            $"結果 {number}",
            $"{resultSet.RowCount.ToString("N0", CultureInfo.InvariantCulture)} 行",
            resultSet,
            string.Empty);

    public static QueryTabViewModel ForMessages(string text) => new("メッセージ", null, null, text);
}
