namespace SQLForge.Application.Security;

/// <summary>
/// サーバーへ送る前に弾いたサーバー ロールの操作。理由はそのままダイアログのメッセージに出す。
/// </summary>
public sealed class ServerRoleRejectedException(string message) : InvalidOperationException(message);
