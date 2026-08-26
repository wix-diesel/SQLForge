namespace SQLForge.Ui.ViewModels.Explorer;

/// <summary>
/// オブジェクトエクスプローラーの右クリックからアプリのライフサイクルへつなぐ口。
/// <see cref="Workspace.IQueryLauncher"/> と同じ考えで、ツリーは「接続を解除したい」
/// とだけ伝えればよく、セッションを閉じて起動画面へ戻す手順は受け取った側の責任にする。
/// </summary>
public interface IConnectionLauncher
{
    /// <summary>今の接続を解除し、起動時の接続ダイアログへ戻る。</summary>
    void Disconnect();
}
