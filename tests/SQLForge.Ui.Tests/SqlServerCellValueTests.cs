using SQLForge.Application.Editing;
using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// グリッドのセル（表示用の文字列）から SQL Server の値への写し。
///
/// 文字列のままパラメータに載せると、bit の「True」のように暗黙変換が効かない型で崩れる。
/// グリッドに出ている書式（<c>AdoValueText</c> が作ったもの）をそのまま読み戻せることを見る。
/// </summary>
public class SqlServerCellValueTests
{
    [Theory]
    [InlineData("int", "42", 42L)]
    [InlineData("bigint", "-1", -1L)]
    [InlineData("nvarchar", "42", "42")]
    [InlineData("bit", "True", true)]
    [InlineData("bit", "False", false)]
    [InlineData("bit", "1", true)]
    [InlineData("bit", "0", false)]
    public void 表示のままの文字列を値へ戻す(string type, string text, object expected) =>
        Assert.Equal(expected, SqlServerCellValue.ToParameter(type, type, text));

    [Fact]
    public void 数と日時と一意識別子はそれぞれの型で渡す()
    {
        Assert.Equal(12.5m, SqlServerCellValue.ToParameter("decimal", "decimal(18, 2)", "12.5"));
        Assert.Equal(
            new DateTime(2026, 8, 26, 9, 30, 0, DateTimeKind.Unspecified),
            SqlServerCellValue.ToParameter("datetime2", "datetime2(7)", "2026-08-26 09:30:00.000"));
        Assert.Equal(
            Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"),
            SqlServerCellValue.ToParameter("uniqueidentifier", "uniqueidentifier", "6f9619ff-8b86-d011-b42d-00c04fc964ff"));
    }

    [Fact]
    public void NULLはそのままNULLで渡す() => Assert.Null(SqlServerCellValue.ToParameter("int", "int", null));

    [Fact]
    public void 型に合わない文字列は弾く()
    {
        Assert.Throws<TableEditRejectedException>(() => SqlServerCellValue.ToParameter("int", "int", "七"));
        Assert.Throws<TableEditRejectedException>(() => SqlServerCellValue.ToParameter("bit", "bit", "はい"));
    }

    [Theory]
    [InlineData("varbinary")]
    [InlineData("image")]
    [InlineData("text")]
    [InlineData("xml")]
    [InlineData("timestamp")]
    [InlineData("geography")]
    public void グリッドで扱えない型は書き換えられない(string type)
    {
        // SSMS の編集グリッドでも、これらの列は読むだけになる。
        Assert.False(SqlServerCellValue.IsEditable(type));
        Assert.Throws<TableEditRejectedException>(() => SqlServerCellValue.ToParameter(type, type, "0x00"));
    }

    [Fact]
    public void 比較できる型だけを行の特定に使う()
    {
        // 主キーが無いテーブルでは、比較できる列を並べて 1 行に絞り込む。
        Assert.True(SqlServerCellValue.IsComparable("int"));
        Assert.True(SqlServerCellValue.IsComparable("nvarchar"));
        Assert.False(SqlServerCellValue.IsComparable("ntext"));
    }

    [Fact]
    public void 数値の列だけを右へ寄せる()
    {
        Assert.True(SqlServerCellValue.IsNumeric("decimal"));
        Assert.False(SqlServerCellValue.IsNumeric("nvarchar"));
        Assert.True(SqlServerCellValue.IsText("nvarchar"));
        Assert.False(SqlServerCellValue.IsText("int"));
    }
}
