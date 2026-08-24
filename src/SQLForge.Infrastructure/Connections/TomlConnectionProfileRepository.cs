using SQLForge.Application.Abstractions;
using SQLForge.Domain.Connections;

namespace SQLForge.Infrastructure.Connections;

/// <summary>
/// 保存済み接続を TOML ファイル 1 枚に置く実装（Linux なら
/// <c>~/.config/sqlforge/connections.toml</c>）。置き場所は OS ごとの体裁
/// <see cref="IPlatformProfile.ProfileDirectory"/> が決める。
///
/// パスワードはここには書かない。資格情報は <see cref="ISecretStore"/> 側で
/// OS のキーリングへ預ける。
/// </summary>
public sealed class TomlConnectionProfileRepository : IConnectionProfileRepository
{
    private const string FileName = "connections.toml";

    /// <summary>本人だけが読み書きできる権限（Unix の 0600 / 0700）。Windows では使わない。</summary>
    private const UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerOnlyDirectory = OwnerOnlyFile | UnixFileMode.UserExecute;

    // 読み書きのたびにファイルを開くので、同時に走らせない。
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _directory;

    public TomlConnectionProfileRepository(IPlatformProfile platform)
        : this((platform ?? throw new ArgumentNullException(nameof(platform))).ProfileDirectory)
    {
    }

    private TomlConnectionProfileRepository(string directory)
    {
        _directory = directory;
        FilePath = Path.Combine(directory, FileName);
    }

    /// <summary>置き場所を指定して作る（テスト用）。</summary>
    public static TomlConnectionProfileRepository At(string directory) => new(directory);

    public string FilePath { get; }

    public async Task<IReadOnlyList<ConnectionProfile>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ConnectionProfile?> FindAsync(
        ConnectionProfileId id,
        CancellationToken cancellationToken = default)
    {
        var profiles = await ListAsync(cancellationToken).ConfigureAwait(false);

        return profiles.FirstOrDefault(profile => profile.Id == id);
    }

    public Task SaveAsync(ConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return UpdateAsync(profiles =>
        {
            var index = profiles.FindIndex(existing => existing.Id == profile.Id);
            if (index >= 0)
            {
                profiles[index] = profile;
                return;
            }

            profiles.Add(profile);
        }, cancellationToken);
    }

    public Task DeleteAsync(ConnectionProfileId id, CancellationToken cancellationToken = default) =>
        UpdateAsync(profiles => profiles.RemoveAll(profile => profile.Id == id), cancellationToken);

    private async Task UpdateAsync(Action<List<ConnectionProfile>> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var profiles = (await ReadAsync(cancellationToken).ConfigureAwait(false)).ToList();
            change(profiles);
            await WriteAsync(profiles, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<ConnectionProfile>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        var text = await File.ReadAllTextAsync(FilePath, cancellationToken).ConfigureAwait(false);

        try
        {
            return ConnectionProfileToml.Read(text);
        }
        catch (FormatException exception)
        {
            // 読めないファイルを黙って空扱いにすると、保存済み接続が消えたように見えてしまう。
            throw new FormatException($"{FilePath} を読めません。{exception.Message}", exception);
        }
    }

    private async Task WriteAsync(IReadOnlyList<ConnectionProfile> profiles, CancellationToken cancellationToken)
    {
        CreateDirectory();

        // 書いている途中で落ちても元のファイルを壊さないよう、別名で書いてから置き換える。
        var temporary = FilePath + ".tmp";
        await File.WriteAllTextAsync(temporary, ConnectionProfileToml.Write(profiles), cancellationToken)
            .ConfigureAwait(false);
        Restrict(temporary);
        File.Move(temporary, FilePath, overwrite: true);
    }

    private void CreateDirectory()
    {
        if (Directory.Exists(_directory))
        {
            return;
        }

        // 作るときに権限まで決める（作ってから絞ると、その隙間に他人が覗ける）。
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(_directory);
            return;
        }

        Directory.CreateDirectory(_directory, OwnerOnlyDirectory);
    }

    /// <summary>接続情報は本人以外に見せない。Windows の権限は継承した ACL に任せる。</summary>
    private static void Restrict(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, OwnerOnlyFile);
        }
    }
}
