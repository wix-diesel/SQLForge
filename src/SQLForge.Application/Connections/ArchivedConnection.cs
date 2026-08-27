using SQLForge.Domain.Connections;

namespace SQLForge.Application.Connections;

/// <summary>
/// 書き出し・取り込みで運ぶ接続 1 件。
///
/// パスワードが入るのは「ユーザー名とパスワードも書き出す」を選んだときだけで、
/// ふだんの保存先（<c>connections.toml</c>）と同じく、既定では
/// <see cref="Secret"/> は <c>null</c> のまま運ぶ。
/// </summary>
public sealed record ArchivedConnection(ConnectionProfile Profile, string? Secret);
