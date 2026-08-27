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

        var text = ConnectionProfileToml.WriteArchive(connections);

        await using var stream = Create(path);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(text.AsMemory(), cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// 中身を書く前に権限を決める。作ってから絞ると、その隙間（umask 次第では
    /// 他人も読める権限のまま）にパスワードを覗かれうる。
    /// Windows の権限は継承した ACL に任せる。
    /// </summary>
    private static FileStream Create(string path)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Options = FileOptions.Asynchronous
        };

        if (OperatingSystem.IsWindows())
        {
            return new FileStream(path, options);
        }

        options.UnixCreateMode = OwnerOnlyFile;
        var stream = new FileStream(path, options);

        // UnixCreateMode が効くのは作るときだけなので、すでにあったファイルは
        // 空にしたところ（まだ何も書いていないうち）で絞り直す。
        File.SetUnixFileMode(stream.SafeFileHandle, OwnerOnlyFile);

        return stream;
    }
}
