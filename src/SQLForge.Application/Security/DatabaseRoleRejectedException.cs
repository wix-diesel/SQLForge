namespace SQLForge.Application.Security;

/// <summary>
/// サーバーへ送る前に弾いたデータベース ロールの操作。理由はそのままダイアログのメッセージに出す。
/// </summary>
public sealed class DatabaseRoleRejectedException(string message) : InvalidOperationException(message);
