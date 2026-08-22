namespace SQLForge.Application.Abstractions;

/// <summary>
/// 資格情報の保管ポート。Linux は Secret Service、Windows は資格情報マネージャー、
/// macOS はキーチェーンを想定する。読めない環境では都度入力にフォールバックする。
/// </summary>
public interface ISecretStore
{
    /// <summary>この環境でキーリングが使えるか。使えなければ保存トグルを無効にする。</summary>
    bool IsAvailable { get; }

    /// <summary>「キーリングに保存」の横に出す説明（保管先の名前）。</summary>
    string DisplayName { get; }

    Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default);

    Task<string?> ReadAsync(string key, CancellationToken cancellationToken = default);
}
