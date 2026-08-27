using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 書き出し・取り込みのファイルを、ふだんの保存先（<see cref="TomlConnectionProfileRepository"/>）と
/// 同じ TOML の形で読み書きする実装。形が同じなので、書き出したファイルをそのまま
/// <c>connections.toml</c> として置くこともできる。
///
/// 書き出したファイルは、パスワードを含めていなくても「どこへ誰として繋ぐか」が並んだ表なので、
/// ふだんの保存先と同じく本人だけが読める権限で置く。
/// </summary>
public sealed class TomlConnectionArchive : IConnectionArchive
{
    /// <summary>本人だけが読み書きできる権限（Unix の 0600）。Windows では使わない。</summary>
    private const UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public async Task WriteAsync(
        string path,
        IReadOnlyList<ArchivedConnection> connections,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(connections);

        await File.WriteAllTextAsync(path, ConnectionProfileToml.WriteArchive(connections), cancellationToken)
            .ConfigureAwait(false);
        Restrict(path);
    }

    public async Task<IReadOnlyList<ArchivedConnection>> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

        try
        {
            return ConnectionProfileToml.ReadArchive(text);
        }
        catch (FormatException exception)
        {
            // 取り違えたファイルを選んだときに、どこが読めないのかまで出す。
            throw new FormatException($"{path} を読めません。{exception.Message}", exception);
        }
    }

    private static void Restrict(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, OwnerOnlyFile);
        }
    }
}
