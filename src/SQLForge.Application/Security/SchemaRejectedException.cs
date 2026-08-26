namespace SQLForge.Application.Security;

/// <summary>
/// サーバーへ送る前に弾いたスキーマの操作。理由はそのままダイアログのメッセージに出す。
/// </summary>
public sealed class SchemaRejectedException(string message) : InvalidOperationException(message);
