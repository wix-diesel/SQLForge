namespace SQLForge.Application.Security;

/// <summary>
/// サーバーへ送る前に弾いたユーザー操作。理由はそのままダイアログのメッセージに出す。
/// </summary>
public sealed class DatabaseUserRejectedException(string message) : InvalidOperationException(message);
