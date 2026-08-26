namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// パラメータ付きの文 1 つ。ドライバーが組み立て、<see cref="AdoDatabaseSession"/> が流す。
///
/// 値はすべてパラメータで渡す（文面に埋めない）。名前は並びから決め打ちで
/// <c>@p0</c>、<c>@p1</c>… とするので、組み立てる側も同じ名前で書く。
/// 識別子はパラメータにできないので、そちらは組み立てる側で引用符を付ける。
/// </summary>
/// <param name="Text">実行する文面。</param>
/// <param name="Parameters">@p0 から順に割り当てる値。null は SQL の NULL。</param>
public sealed record ParameterizedStatement(string Text, IReadOnlyList<object?> Parameters)
{
    /// <summary>並びの <paramref name="ordinal"/> 番目に対応するパラメータ名。</summary>
    public static string NameOf(int ordinal) => $"@p{ordinal}";
}
