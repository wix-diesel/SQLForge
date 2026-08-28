namespace SQLForge.Domain.Filtering;

/// <summary>
/// オブジェクトエクスプローラーの絞り込みで条件にできるプロパティ。
/// SSMS の「フィルターの設定」に並ぶ行そのもので、どれが並ぶかは見出しごとに決まる
/// （テーブルなら名前と作成日、ユーザーなら名前だけ）。
/// </summary>
public enum ObjectFilterProperty
{
    /// <summary>オブジェクトの名前。</summary>
    Name,

    /// <summary>作成された日。エンジンから読めるものだけが条件に出す。</summary>
    CreatedAt
}

/// <summary>プロパティごとの表示名と説明。SSMS のダイアログの並びと文言に合わせる。</summary>
public static class ObjectFilterProperties
{
    public static string DisplayName(this ObjectFilterProperty property) => property switch
    {
        ObjectFilterProperty.Name => "名前",
        ObjectFilterProperty.CreatedAt => "作成日",
        _ => "不明なプロパティ"
    };

    /// <summary>ダイアログの下に出す説明。選んでいる行のものを出す。</summary>
    public static string Description(this ObjectFilterProperty property) => property switch
    {
        ObjectFilterProperty.Name =>
            "名前で絞り込みます。大文字と小文字は区別しません。値を空にすると、この条件は使いません。",
        ObjectFilterProperty.CreatedAt =>
            "作成された日で絞り込みます。日付は yyyy/MM/dd の形で入力します。値を空にすると、この条件は使いません。",
        _ => string.Empty
    };

    /// <summary>日付として扱うプロパティか。文字列の演算子ではなく日付の演算子を並べる。</summary>
    public static bool IsDate(this ObjectFilterProperty property) => property == ObjectFilterProperty.CreatedAt;
}
