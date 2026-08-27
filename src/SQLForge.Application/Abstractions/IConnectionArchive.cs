using SQLForge.Application.Connections;

namespace SQLForge.Application.Abstractions;

/// <summary>
/// 保存済み接続の書き出し・取り込みで使う「持ち運べる 1 枚のファイル」のポート。
/// どんな形で書くか（TOML か XML か）は Infrastructure 側の受け持ち。
/// </summary>
public interface IConnectionArchive
{
    /// <summary>指定の場所へ書き出す。同じ名前のファイルがあれば置き換える。</summary>
    Task WriteAsync(
        string path,
        IReadOnlyList<ArchivedConnection> connections,
        CancellationToken cancellationToken = default);

    /// <summary>書き出したファイルを読む。読めない中身は <see cref="FormatException"/> で伝える。</summary>
    Task<IReadOnlyList<ArchivedConnection>> ReadAsync(string path, CancellationToken cancellationToken = default);
}
