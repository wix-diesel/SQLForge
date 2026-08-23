using SQLForge.Infrastructure.SqlServer;
using Xunit;

namespace SQLForge.Ui.Tests;

/// <summary>
/// カラムの型の表示名。SSMS に合わせて、可変長の型は長さを、decimal 系は精度と位取りを添える。
/// </summary>
public class SqlServerTypeFormatTests
{
    [Fact]
    public void byte単位の可変長型はそのままの長さを出す()
    {
        Assert.Equal("varchar(50)", SqlServerTypeFormat.Describe("varchar", 50, 0, 0));
        Assert.Equal("varchar(max)", SqlServerTypeFormat.Describe("varchar", -1, 0, 0));
    }

    [Fact]
    public void nvarchar系はバイト数を文字数へ半分にして出す()
    {
        Assert.Equal("nvarchar(50)", SqlServerTypeFormat.Describe("nvarchar", 100, 0, 0));
        Assert.Equal("nvarchar(max)", SqlServerTypeFormat.Describe("nvarchar", -1, 0, 0));
    }

    [Fact]
    public void decimal系は精度と位取りを出す()
    {
        Assert.Equal("decimal(18, 2)", SqlServerTypeFormat.Describe("decimal", 9, 18, 2));
    }

    [Fact]
    public void 時刻系は位取りだけ出す()
    {
        Assert.Equal("datetime2(7)", SqlServerTypeFormat.Describe("datetime2", 8, 0, 7));
    }

    [Fact]
    public void それ以外は型名だけ出す()
    {
        Assert.Equal("int", SqlServerTypeFormat.Describe("int", 4, 10, 0));
    }
}
