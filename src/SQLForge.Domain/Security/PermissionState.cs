namespace SQLForge.Domain.Security;

/// <summary>
/// 権限 1 つの状態。SSMS の権限グリッドの「許可」「許可の付与」「拒否」の 3 つの
/// チェックボックスに対応し、どれも付いていない状態が <see cref="Revoked"/> になる。
/// </summary>
public enum PermissionState
{
    /// <summary>明示的な指定なし。すでに付いているものを外すときは REVOKE を出す。</summary>
    Revoked,

    /// <summary>許可（GRANT）。</summary>
    Granted,

    /// <summary>許可の付与（GRANT ... WITH GRANT OPTION）。他人へ渡す権利まで持たせる。</summary>
    GrantedWithGrantOption,

    /// <summary>拒否（DENY）。ロール経由の許可より強い。</summary>
    Denied
}

/// <summary>状態ごとの表示名。</summary>
public static class PermissionStates
{
    /// <summary>グリッドの選択肢の並び。SSMS の列の並びに合わせる。</summary>
    public static IReadOnlyList<PermissionState> All { get; } =
    [
        PermissionState.Revoked,
        PermissionState.Granted,
        PermissionState.GrantedWithGrantOption,
        PermissionState.Denied
    ];

    public static string DisplayName(this PermissionState state) => state switch
    {
        PermissionState.Revoked => "指定なし",
        PermissionState.Granted => "許可",
        PermissionState.GrantedWithGrantOption => "許可の付与",
        PermissionState.Denied => "拒否",
        _ => "不明な状態"
    };

    /// <summary>サーバーへ何か送る必要がある状態か。</summary>
    public static bool IsExplicit(this PermissionState state) => state is not PermissionState.Revoked;
}
