namespace SQLForge.Ui.Presentation;

/// <summary>
/// 結果グリッドの列幅。列はクエリを実行するまで分からないので、
/// 見出しと中身の文字数から 1 度だけ決める。
///
/// ヘッダーのセルとデータのセルが同じ値を引くことで列が揃う。列幅を測る側を
/// 1 か所に閉じ込めておかないと、行を仮想化したときに見出しと中身がずれる。
/// </summary>
public static class ResultColumnWidth
{
    private const double Min = 72;
    private const double Max = 320;

    /// <summary>等幅 12px のだいたいの 1 文字幅。</summary>
    private const double CharacterWidth = 7.2;

    /// <summary>セルの左右の余白と区切り線のぶん。</summary>
    private const double Padding = 22;

    /// <summary>幅を決めるのに見る行数。全部見ても幅はほとんど変わらないので頭だけ見る。</summary>
    private const int SampleRows = 200;

    public static double For(string header, string typeName, IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        // 型名は見出しの右に小さく添えるので、文字数をそのままは数えない。
        var widest = Columns(header) + (int)Math.Ceiling(Columns(typeName) * 0.8);

        foreach (var value in values.Take(SampleRows))
        {
            widest = Math.Max(widest, Columns(value ?? "NULL"));
        }

        return Math.Clamp((widest * CharacterWidth) + Padding, Min, Max);
    }

    /// <summary>全角は 2 文字ぶんとして数える（「北米」と「NA」は同じ 2 文字でも幅が違う）。</summary>
    private static int Columns(string text)
    {
        var width = 0;

        foreach (var character in text)
        {
            // U+1100 から先はハングル・CJK・全角記号。細かく分けても幅の見積りは変わらない。
            width += character >= '\u1100' ? 2 : 1;
        }

        return width;
    }
}
