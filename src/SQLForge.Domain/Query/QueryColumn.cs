namespace SQLForge.Domain.Query;

/// <summary>
/// 結果セットの列 1 つ。
/// </summary>
/// <param name="Name">列名。名前のない式には「(列 3)」のような代わりの名前が入る。</param>
/// <param name="TypeName">エンジンが返した型名（int、nvarchar など）。</param>
/// <param name="IsNumeric">数値列か。グリッドで右へ寄せるかどうかだけに使う。</param>
public sealed record QueryColumn(string Name, string TypeName, bool IsNumeric);
