namespace SQLForge.Application.Security;

/// <summary>
/// サーバーへ送る前に弾いたログイン操作。理由はそのままダイアログのメッセージに出す。
/// </summary>
public sealed class ServerLoginRejectedException(string message) : InvalidOperationException(message);
