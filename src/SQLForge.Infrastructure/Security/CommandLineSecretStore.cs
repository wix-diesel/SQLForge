using System.Diagnostics;
using System.Text;

namespace SQLForge.Infrastructure.Security;

/// <summary>外部コマンドの実行結果。</summary>
/// <param name="ExitCode">終了コード。「見つからない」を表す値は OS ごとに違う。</param>
public readonly record struct CommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// OS 付属のコマンド（Linux の <c>secret-tool</c>、macOS の <c>security</c>）ごしに
/// キーリングを使う実装の共通部分。
///
/// パスワードは可能なかぎり標準入力から渡す。コマンドラインの引数は
/// 同じ機械の他のプロセスから見えるため。
/// </summary>
public abstract class CommandLineSecretStore : PlatformSecretStore
{
    /// <summary>キーリングの中で SQLForge の預かり物を見分ける名前。</summary>
    protected const string ServiceName = "sqlforge";

    protected static async Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? input,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(fileName)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"{fileName} を起動できません。");

        if (input is not null)
        {
            await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new CommandResult(process.ExitCode, output, error);
    }

    /// <summary>コマンドが入っているか。入っていなければキーリングは使えないものとして扱う。</summary>
    protected static bool ExistsOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(directory => SafeExists(directory, fileName));
    }

    private static bool SafeExists(string directory, string fileName)
    {
        try
        {
            return File.Exists(Path.Combine(directory, fileName));
        }
        catch (ArgumentException)
        {
            // PATH に紛れ込んだ、パスとして成り立たない断片は無視する。
            return false;
        }
    }
}
