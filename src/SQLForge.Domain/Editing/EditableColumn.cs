namespace SQLForge.Domain.Editing;

/// <summary>
/// 編集グリッドの列 1 つ。
///
/// カタログの <see cref="Catalog.ColumnDescriptor"/> と似ているが、こちらは
/// 「この列を書き換えられるか」「この列で行を特定できるか」という編集の都合だけを持つ。
/// どの型を編集できるか・どの列を鍵にするかはエンジンごとに違うので、判断は読み取り側が済ませ、
/// ここには結果だけが入る。
/// </summary>
/// <param name="Name">列名。</param>
/// <param name="DataType">型の表示名（例: nvarchar(50)）。見出しの右に添える。</param>
/// <param name="IsNullable">NULL を許すか。</param>
/// <param name="IsKey">
/// 更新するときに行を特定する条件に使う列か。主キーがあればその列、無ければ
/// 比較できる列すべて（SSMS が主キーの無いテーブルでするのと同じ）。
/// </param>
/// <param name="IsReadOnly">
/// 書き換えられない列か。IDENTITY・計算列・rowversion と、
/// グリッドで扱えない型（binary、xml など）が該当する。
/// </param>
/// <param name="IsNumeric">数値列か。グリッドで右へ寄せるかどうかだけに使う。</param>
/// <param name="IsText">
/// 文字列の列か。空欄を確定したときに、空文字列と NULL のどちらにするかを分ける
/// （文字列なら空文字列、それ以外なら NULL。SSMS と同じ）。
/// </param>
/// <param name="IsIdentity">
/// サーバーが採番する列か（IDENTITY など）。<see cref="IsReadOnly"/> にも含まれるが、
/// 行を足したあとに「いま入った行」を読み直す条件を組むのに、採番かどうかだけを別に持つ。
/// </param>
public sealed record EditableColumn(
    string Name,
    string DataType,
    bool IsNullable,
    bool IsKey,
    bool IsReadOnly,
    bool IsNumeric,
    bool IsText,
    bool IsIdentity = false);
