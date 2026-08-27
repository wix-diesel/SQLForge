using SQLForge.Application.Abstractions;
using SQLForge.Application.Connections;

namespace SQLForge.Ui.Tests;

/// <summary>
/// 書き出し・取り込みのファイルの差し替え。ディスクを触らず、
/// 書き出した中身をそのまま覗けるようにする。
/// </summary>
public sealed class FakeConnectionArchive : IConnectionArchive
{
    /// <summary>直前に書き出した中身。</summary>
    public IReadOnlyList<ArchivedConnection> Written { get; private set; } = [];

    /// <summary>書き出したことにする場所。</summary>
    public string? WrittenTo { get; private set; }

    /// <summary>読ませたい中身。</summary>
    public IReadOnlyList<ArchivedConnection> Stored { get; set; } = [];

    /// <summary>読もうとしたときに投げさせたい失敗。</summary>
    public Exception? ReadFailure { get; set; }

    public Task WriteAsync(
        string path,
        IReadOnlyList<ArchivedConnection> connections,
        CancellationToken cancellationToken = default)
    {
        WrittenTo = path;
        Written = connections;

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ArchivedConnection>> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        ReadFailure is { } failure ? Task.FromException<IReadOnlyList<ArchivedConnection>>(failure) : Task.FromResult(Stored);
}
