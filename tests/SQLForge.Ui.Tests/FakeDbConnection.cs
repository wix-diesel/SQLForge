using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace SQLForge.Ui.Tests;

/// <summary>
/// ADO.NET の口だけを満たす接続。実サーバー無しで、
/// <see cref="SQLForge.Infrastructure.Connections.AdoDatabaseSession"/> が組んだ文面と、
/// 返ってきた行の扱いを確かめるために使う。
/// </summary>
public sealed class FakeDbConnection : DbConnection
{
    /// <summary>読み取りで返す行。列の並びは求められた順そのものとして扱う。</summary>
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; set; } = [];

    /// <summary>更新が返す行数。2 以上にすると「条件に複数行が当たった」を作れる。</summary>
    public int AffectedRows { get; set; } = 1;

    /// <summary>流された文。順番に残る。</summary>
    public List<FakeDbCommand> Commands { get; } = [];

    public FakeDbTransaction? Transaction { get; private set; }

    [AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;

    public override string Database => string.Empty;

    public override string DataSource => string.Empty;

    public override string ServerVersion => string.Empty;

    public override ConnectionState State => ConnectionState.Open;

    public override void ChangeDatabase(string databaseName)
    {
    }

    public override void Close()
    {
    }

    public override void Open()
    {
    }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        Transaction = new FakeDbTransaction(this);

    protected override DbCommand CreateDbCommand()
    {
        var command = new FakeDbCommand(this);
        Commands.Add(command);

        return command;
    }
}

/// <summary>流された文 1 つ。文面とパラメータの値を残す。</summary>
public sealed class FakeDbCommand(FakeDbConnection connection) : DbCommand
{
    private readonly FakeDbParameterCollection _parameters = [];

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    /// <summary>@p0 から順に渡された値。</summary>
    public IReadOnlyList<object?> Values => _parameters.Values;

    protected override DbConnection? DbConnection { get; set; } = connection;

    protected override DbParameterCollection DbParameterCollection => _parameters;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery() => connection.AffectedRows;

    public override object? ExecuteScalar() => null;

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => new FakeDbParameter();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) =>
        new FakeDbDataReader(connection.Rows);
}

/// <summary>コミットと巻き戻しのどちらへ辿り着いたかを残すトランザクション。</summary>
public sealed class FakeDbTransaction(FakeDbConnection connection) : DbTransaction
{
    public bool IsCommitted { get; private set; }

    public bool IsRolledBack { get; private set; }

    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

    protected override DbConnection DbConnection => connection;

    public override void Commit() => IsCommitted = true;

    public override void Rollback() => IsRolledBack = true;
}

/// <summary>決め打ちの行を順に返すリーダー。値の型はそのまま渡す。</summary>
public sealed class FakeDbDataReader(IReadOnlyList<IReadOnlyList<object?>> rows) : DbDataReader
{
    private int _index = -1;

    public override int FieldCount => rows.Count > 0 ? rows[0].Count : 0;

    public override bool HasRows => rows.Count > 0;

    public override bool IsClosed { get; }

    public override int RecordsAffected => -1;

    public override int Depth => 0;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => throw new NotSupportedException();

    public override bool Read() => ++_index < rows.Count;

    public override bool NextResult() => false;

    public override object GetValue(int ordinal) => rows[_index][ordinal] ?? DBNull.Value;

    public override bool IsDBNull(int ordinal) => rows[_index][ordinal] is null;

    public override string GetName(int ordinal) => $"c{ordinal}";

    public override string GetDataTypeName(int ordinal) => "nvarchar";

    public override Type GetFieldType(int ordinal) => typeof(string);

    public override int GetOrdinal(string name) => throw new NotSupportedException();

    public override int GetValues(object[] values) => throw new NotSupportedException();

    public override IEnumerator GetEnumerator() => throw new NotSupportedException();

    public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

    public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override char GetChar(int ordinal) => (char)GetValue(ordinal);

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        throw new NotSupportedException();

    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

    public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);

    public override double GetDouble(int ordinal) => (double)GetValue(ordinal);

    public override float GetFloat(int ordinal) => (float)GetValue(ordinal);

    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

    public override short GetInt16(int ordinal) => (short)GetValue(ordinal);

    public override int GetInt32(int ordinal) => (int)GetValue(ordinal);

    public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

    public override string GetString(int ordinal) => (string)GetValue(ordinal);
}

/// <summary>名前と値だけのパラメータ。</summary>
public sealed class FakeDbParameter : DbParameter
{
    public override DbType DbType { get; set; }

    public override ParameterDirection Direction { get; set; }

    public override bool IsNullable { get; set; }

    [AllowNull]
    public override string ParameterName { get; set; } = string.Empty;

    public override int Size { get; set; }

    [AllowNull]
    public override string SourceColumn { get; set; } = string.Empty;

    public override bool SourceColumnNullMapping { get; set; }

    public override object? Value { get; set; }

    public override void ResetDbType()
    {
    }
}

/// <summary>並びだけを持つパラメータの入れ物。</summary>
public sealed class FakeDbParameterCollection : DbParameterCollection
{
    private readonly List<DbParameter> _parameters = [];

    public IReadOnlyList<object?> Values => _parameters.Select(parameter => parameter.Value).ToList();

    public override int Count => _parameters.Count;

    public override object SyncRoot { get; } = new();

    public override int Add(object value)
    {
        _parameters.Add((DbParameter)value);

        return _parameters.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }

    public override void Clear() => _parameters.Clear();

    public override bool Contains(object value) => _parameters.Contains((DbParameter)value);

    public override bool Contains(string value) => IndexOf(value) >= 0;

    public override void CopyTo(Array array, int index) => throw new NotSupportedException();

    public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

    public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);

    public override int IndexOf(string parameterName) =>
        _parameters.FindIndex(parameter =>
            string.Equals(parameter.ParameterName, parameterName, StringComparison.Ordinal));

    public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);

    public override void Remove(object value) => _parameters.Remove((DbParameter)value);

    public override void RemoveAt(int index) => _parameters.RemoveAt(index);

    public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => _parameters[index];

    protected override DbParameter GetParameter(string parameterName) => _parameters[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value) =>
        _parameters[IndexOf(parameterName)] = value;
}
