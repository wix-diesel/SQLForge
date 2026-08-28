using System.Globalization;
using System.Text;
using SQLForge.Domain.Query;

namespace SQLForge.Ui.Presentation;

/// <summary>
/// 実行の結果を人の読む文へ写す。ツールバー右端の一行と「メッセージ」タブの本文。
///
/// タブ 1 枚ごとに同じ文を組むので、タブのビューモデルからは離してある。
/// </summary>
public static class QueryOutcomeFormat
{
    /// <summary>ツールバー右端の一行。実行時間と件数だけを出す。</summary>
    public static string Status(QueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var status = new StringBuilder(Duration(result.Elapsed));

        if (result.ResultSets.Count > 0)
        {
            status.Append(" · ").Append(Count(result.TotalRows)).Append(" 行");

            if (result.ResultSets.Any(set => set.IsTruncated))
            {
                status.Append("（上限で打ち切り）");
            }
        }
        else if (result.RecordsAffected >= 0)
        {
            status.Append(" · ").Append(Count(result.RecordsAffected)).Append(" 行処理");
        }

        return status.ToString();
    }

    /// <summary>「メッセージ」タブの本文。SSMS のメッセージ欄にあたる。</summary>
    public static string Messages(QueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var lines = new List<string>();

        for (var index = 0; index < result.ResultSets.Count; index++)
        {
            var set = result.ResultSets[index];
            var label = result.ResultSets.Count > 1 ? $"結果 {index + 1}: " : string.Empty;

            lines.Add(set.IsTruncated
                ? $"{label}({Count(set.Rows.Count)} 行を取得しました。取得上限に達したため、これより後の行は読んでいません)"
                : $"{label}({Count(set.Rows.Count)} 行を取得しました)");
        }

        if (result.RecordsAffected >= 0)
        {
            lines.Add($"({Count(result.RecordsAffected)} 行処理されました)");
        }

        if (lines.Count == 0)
        {
            lines.Add("(行は返りませんでした)");
        }

        lines.Add(string.Empty);
        lines.Add($"実行時間: {Duration(result.Elapsed)}");

        return string.Join("\n", lines);
    }

    private static string Count(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Duration(TimeSpan elapsed) =>
        $"{Math.Round(elapsed.TotalMilliseconds).ToString("N0", CultureInfo.InvariantCulture)} ms";
}
