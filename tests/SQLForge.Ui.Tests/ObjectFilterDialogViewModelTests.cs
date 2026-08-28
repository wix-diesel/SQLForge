using SQLForge.Domain.Filtering;
using SQLForge.Ui.ViewModels.Explorer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 「フィルターの設定」ダイアログ。SSMS と同じで、条件にできるプロパティが 1 行ずつ並び、
/// 値を空にした行は条件にならない。読めない日付は理由を出して閉じない。
/// </summary>
public class ObjectFilterDialogViewModelTests
{
    [Fact]
    public void 条件にできるプロパティが行として並ぶ()
    {
        var dialog = NewDialog();

        Assert.Equal(["名前", "作成日"], dialog.Rows.Select(row => row.DisplayName));

        var name = dialog.Rows[0];
        Assert.False(name.IsDate);
        Assert.Equal("次を含む", name.Operator.DisplayName);
        Assert.Equal(["次を含む", "次を含まない", "次と等しい", "次と等しくない"],
            name.Operators.Select(choice => choice.DisplayName));

        // 開いた直後は 1 行目を選んでおく（下の説明欄が空にならないように）。
        Assert.Same(name, dialog.SelectedRow);
    }

    [Fact]
    public void 値が空の行は条件にならない()
    {
        var dialog = NewDialog();

        Assert.True(Accept(dialog));
        Assert.NotNull(dialog.Result);
        Assert.True(dialog.Result!.IsEmpty);
    }

    [Fact]
    public void 名前の条件を組む()
    {
        var dialog = NewDialog();
        dialog.Rows[0].Operator = FilterOperatorChoiceViewModel.Of(TextFilterOperator.Equal);
        dialog.Rows[0].Value = "  orders  ";

        Assert.True(Accept(dialog));

        var clause = Assert.Single(dialog.Result!.Texts);
        Assert.Equal(TextFilterOperator.Equal, clause.Operator);
        // 前後の空白は落とす（打ち間違いで当たらなくなるのを避ける）。
        Assert.Equal("orders", clause.Value);
        Assert.Null(dialog.Result.CreatedAt);
    }

    [Fact]
    public void 作成日の条件を組む()
    {
        var dialog = NewDialog();
        dialog.Rows[1].Operator = FilterOperatorChoiceViewModel.Of(DateFilterOperator.GreaterThanOrEqual);
        dialog.Rows[1].Value = "2026/04/15";

        Assert.True(Accept(dialog));

        var clause = dialog.Result!.CreatedAt;
        Assert.NotNull(clause);
        Assert.Equal(DateFilterOperator.GreaterThanOrEqual, clause!.Operator);
        Assert.Equal(new DateOnly(2026, 4, 15), clause.Value);
    }

    [Fact]
    public void 読めない日付は理由を出して閉じない()
    {
        var dialog = NewDialog();
        dialog.Rows[1].Value = "きのう";

        Assert.False(Accept(dialog));
        Assert.Null(dialog.Result);
        Assert.True(dialog.HasError);
        Assert.Contains("yyyy/MM/dd", dialog.ErrorMessage!);
    }

    [Fact]
    public void 次の間は終わりの日まで入れて初めて通る()
    {
        var dialog = NewDialog();
        var created = dialog.Rows[1];
        created.Operator = FilterOperatorChoiceViewModel.Of(DateFilterOperator.Between);
        created.Value = "2026/04/01";

        // 「次の間」を選んだら 2 つめの入力欄が出る。
        Assert.True(created.ShowBound);
        Assert.False(Accept(dialog));

        created.Bound = "2026/04/30";

        Assert.True(Accept(dialog));
        Assert.Equal(new DateOnly(2026, 4, 30), dialog.Result!.CreatedAt!.Bound);
    }

    [Fact]
    public void フィルターのクリアで入力が空へ戻る()
    {
        var dialog = NewDialog(NameContains("or"));
        Assert.Equal("or", dialog.Rows[0].Value);

        dialog.ClearCommand.Execute(null);

        Assert.Equal(string.Empty, dialog.Rows[0].Value);
        Assert.Equal("次を含む", dialog.Rows[0].Operator.DisplayName);

        // クリアしただけでは何も起きない。確定するのは OK を押してから。
        Assert.True(Accept(dialog));
        Assert.True(dialog.Result!.IsEmpty);
    }

    [Fact]
    public void 開くと今かかっている条件が入っている()
    {
        var dialog = NewDialog(NameContains("or"));

        Assert.Equal("or", dialog.Rows[0].Value);
        Assert.Equal("次を含む", dialog.Rows[0].Operator.DisplayName);
    }

    [Fact]
    public void キャンセルすると何も返さない()
    {
        var dialog = NewDialog();
        dialog.Rows[0].Value = "orders";

        var closed = false;
        dialog.CloseRequested += (_, result) => closed = !result;
        dialog.CancelCommand.Execute(null);

        Assert.True(closed);
        Assert.Null(dialog.Result);
    }

    private static ObjectFilterDialogViewModel NewDialog(ObjectFilter? current = null) =>
        new(
            "sales_db/dbo/テーブル",
            [ObjectFilterProperty.Name, ObjectFilterProperty.CreatedAt],
            current ?? ObjectFilter.None);

    private static ObjectFilter NameContains(string value) =>
        new([new TextFilterClause(ObjectFilterProperty.Name, TextFilterOperator.Contains, value)]);

    /// <summary>OK を押す。閉じる合図が上がったら true。</summary>
    private static bool Accept(ObjectFilterDialogViewModel dialog)
    {
        var accepted = false;
        void OnClose(object? sender, bool result) => accepted = result;

        dialog.CloseRequested += OnClose;
        dialog.AcceptCommand.Execute(null);
        dialog.CloseRequested -= OnClose;

        return accepted;
    }
}
