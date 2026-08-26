namespace SQLForge.Domain.Security;

/// <summary>
/// 「どのリソースの、どの権限が、どうなっているか」1 行。
/// 主体（誰に）は一覧そのものが 1 人ぶんなので、この行は持たない。
/// </summary>
/// <param name="Securable">権限を付ける相手。</param>
/// <param name="Permission">権限の名前（SELECT・EXECUTE・CONTROL など）。</param>
/// <param name="State">その権限の状態。</param>
public sealed record PermissionEntry(
    SecurableReference Securable,
    string Permission,
    PermissionState State)
{
    /// <summary>
    /// 同じ「相手 × 権限」を指しているか。状態は見ない。
    /// 変更前後の突き合わせ（何を REVOKE して何を GRANT するか）で使う。
    /// </summary>
    public bool IsSameTarget(PermissionEntry other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Securable.Kind == other.Securable.Kind
            && string.Equals(Securable.Name, other.Securable.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Securable.Schema, other.Securable.Schema, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Permission, other.Permission, StringComparison.OrdinalIgnoreCase);
    }
}
