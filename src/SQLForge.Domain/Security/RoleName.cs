namespace SQLForge.Domain.Security;

/// <summary>
/// ロール名。データベース ロールとサーバー ロールで名前の作法は変わらないので器を共有する。
/// 識別子はパラメータ化できず SQL 文へ直接埋め込むしかないので、
/// 埋め込む前にここで形を保証しておく（引用符付けはドライバー側の責務）。
/// </summary>
public readonly record struct RoleName
{
    /// <summary>SQL Server の識別子の上限（sysname = nvarchar(128)）。</summary>
    public const int MaxLength = 128;

    public RoleName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("ロール名は空にできません。", nameof(value));
        }

        if (value.Any(char.IsControl))
        {
            throw new ArgumentException("ロール名に制御文字は使えません。", nameof(value));
        }

        if (value.Length > MaxLength)
        {
            throw new ArgumentException($"ロール名は {MaxLength} 文字までです。", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
