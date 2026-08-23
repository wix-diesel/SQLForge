using SQLForge.Domain.Catalog;

namespace SQLForge.Application.Query;

/// <summary>
/// 実行 1 回ぶんの入力。エディタの文面をそのまま渡す（整形も分割もしない）。
/// </summary>
public sealed record QueryRequest
{
    public QueryRequest(DatabaseName database, string sql, int maxRows = ExecuteQueryUseCase.DefaultMaxRows)
    {
        Database = database;
        Sql = (sql ?? string.Empty).Trim();
        MaxRows = maxRows < 1 ? 1 : maxRows;
    }

    /// <summary>実行先のデータベース。</summary>
    public DatabaseName Database { get; }

    /// <summary>実行する文面。前後の空白は落としてある。</summary>
    public string Sql { get; }

    /// <summary>結果セットごとに読む行数の上限。</summary>
    public int MaxRows { get; }
}
