namespace SQLForge.Application.Query;

/// <summary>
/// サーバーへ送る前に弾いた実行。理由はそのまま画面のメッセージ欄に出す。
/// </summary>
public sealed class QueryRejectedException(string message) : InvalidOperationException(message);
