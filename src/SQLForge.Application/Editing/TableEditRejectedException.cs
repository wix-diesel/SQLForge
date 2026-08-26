namespace SQLForge.Application.Editing;

/// <summary>
/// サーバーへ送る前に弾いた編集。理由はそのままグリッドの下のメッセージに出す。
/// </summary>
public sealed class TableEditRejectedException(string message) : InvalidOperationException(message);
