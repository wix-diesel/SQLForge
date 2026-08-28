namespace SQLForge.Domain.Sql;

/// <summary>
/// 予約語・組み込み関数・組み込み型の語彙表。
///
/// いまは SQL Server (T-SQL) の語彙をもとにした 1 種類だけを持つ。ドライバーごとに
/// 分けるのは 2 つ目のドライバーを実装する時点でよい（そのときは
/// <see cref="Classify"/> をドライバー引数付きにするか、ポートへ出す）。
/// </summary>
public static class SqlKeywords
{
    private static readonly HashSet<string> KeywordSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADD", "ALL", "ALTER", "AND", "ANY", "APPLY", "AS", "ASC", "AUTHORIZATION",
        "BACKUP", "BEGIN", "BETWEEN", "BREAK", "BULK", "BY",
        "CASCADE", "CASE", "CATCH", "CHECK", "CHECKPOINT", "CLOSE", "CLUSTERED", "COALESCE",
        "COLLATE", "COLUMN", "COMMIT", "CONSTRAINT", "CONTINUE", "CREATE", "CROSS", "CURRENT",
        "CURSOR", "DATABASE", "DBCC", "DEALLOCATE", "DECLARE", "DEFAULT", "DELETE", "DENY",
        "DESC", "DISABLE", "DISTINCT", "DROP", "ELSE", "ENABLE", "END", "ESCAPE", "EXCEPT",
        "EXEC", "EXECUTE", "EXISTS", "EXTERNAL", "FETCH", "FILLFACTOR", "FOR", "FOREIGN",
        "FROM", "FULL", "FUNCTION", "GO", "GOTO", "GRANT", "GROUP", "HAVING", "IDENTITY",
        "IF", "IN", "INCLUDE", "INDEX", "INNER", "INSERT", "INTERSECT", "INTO", "IS", "JOIN",
        "KEY", "LEFT", "LIKE", "LIMIT", "MERGE", "NEXT", "NOCHECK", "NONCLUSTERED", "NOT",
        "NULL", "OF", "OFF", "OFFSET", "ON", "ONLY", "OPEN", "OPTION", "OR", "ORDER", "OUTER",
        "OUTPUT", "OVER", "PARTITION", "PERCENT", "PIVOT", "PRIMARY", "PRINT", "PROC",
        "PROCEDURE", "PUBLIC", "RAISERROR", "READ", "REFERENCES", "RETURN", "REVERT", "REVOKE",
        "RIGHT", "ROLLBACK", "ROW", "ROWCOUNT", "ROWS", "SAVE", "SCHEMA", "SELECT", "SET",
        "SETUSER", "SOME", "STATISTICS", "TABLE", "THEN", "THROW", "TIES", "TO", "TOP",
        "TRAN", "TRANSACTION", "TRIGGER", "TRUNCATE", "TRY", "UNION", "UNIQUE", "UNPIVOT",
        "UPDATE", "USE", "USER", "USING", "VALUES", "VIEW", "WAITFOR", "WHEN", "WHERE",
        "WHILE", "WITH", "WITHIN"
    };

    private static readonly HashSet<string> FunctionSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "ABS", "AVG", "CAST", "CEILING", "CHARINDEX", "CHECKSUM", "CONCAT", "CONCAT_WS",
        "CONVERT", "COUNT", "COUNT_BIG", "CURRENT_TIMESTAMP", "CURRENT_USER", "DATEADD",
        "DATEDIFF", "DATEFROMPARTS", "DATENAME", "DATEPART", "DAY", "DENSE_RANK", "EOMONTH",
        "FLOOR", "FORMAT", "GETDATE", "GETUTCDATE", "IIF", "ISDATE", "ISNULL", "ISNUMERIC",
        "LAG", "LEAD", "LEN", "LOWER", "LTRIM", "MAX", "MIN", "MONTH", "NEWID",
        "NTILE", "NULLIF", "OBJECT_ID", "OBJECT_NAME", "PARSENAME", "PATINDEX", "POWER",
        "QUOTENAME", "RAND", "RANK", "REPLACE", "REPLICATE", "REVERSE", "ROUND", "ROW_NUMBER",
        "RTRIM", "SCOPE_IDENTITY", "SESSION_USER", "SIGN", "SPACE", "SQRT", "STDEV", "STR",
        "STRING_AGG", "STRING_SPLIT", "STUFF", "SUBSTRING", "SUM", "SUSER_NAME", "SUSER_SNAME",
        "SWITCHOFFSET", "SYSDATETIME", "SYSDATETIMEOFFSET", "SYSTEM_USER", "TODATETIMEOFFSET",
        "TRIM", "TRY_CAST", "TRY_CONVERT", "TRY_PARSE", "UPPER", "USER_NAME", "VAR", "YEAR"
    };

    private static readonly HashSet<string> TypeSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIGINT", "BINARY", "BIT", "CHAR", "DATE", "DATETIME", "DATETIME2", "DATETIMEOFFSET",
        "DECIMAL", "FLOAT", "GEOGRAPHY", "GEOMETRY", "HIERARCHYID", "IMAGE", "INT", "INTEGER",
        "MONEY", "NCHAR", "NTEXT", "NUMERIC", "NVARCHAR", "REAL", "ROWVERSION", "SMALLDATETIME",
        "SMALLINT", "SMALLMONEY", "SQL_VARIANT", "TEXT", "TIME", "TIMESTAMP", "TINYINT",
        "UNIQUEIDENTIFIER", "VARBINARY", "VARCHAR", "XML"
    };

    /// <summary>補完の候補に出す予約語。並びは呼び出し側で決める。</summary>
    public static IReadOnlyCollection<string> Keywords => KeywordSet;

    /// <summary>補完の候補に出す組み込み関数。</summary>
    public static IReadOnlyCollection<string> Functions => FunctionSet;

    /// <summary>語 1 つの種類を決める。表に無ければ識別子として扱う。</summary>
    public static SqlTokenKind Classify(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        if (KeywordSet.Contains(word))
        {
            return SqlTokenKind.Keyword;
        }

        if (FunctionSet.Contains(word))
        {
            return SqlTokenKind.Function;
        }

        return TypeSet.Contains(word) ? SqlTokenKind.Type : SqlTokenKind.Identifier;
    }

    /// <summary>予約語か。引用符なしで識別子として使えるかの判定に使う。</summary>
    public static bool IsKeyword(string word) => word is not null && KeywordSet.Contains(word);
}
