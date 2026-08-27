namespace SQLForge.Application.Connections;

/// <summary>
/// SSH トンネルを開けなかった。理由はそのままダイアログのフッターへ出すので、
/// 利用者が次に何を直せばよいか分かる文面で投げること。
/// </summary>
public sealed class SshTunnelException : Exception
{
    public SshTunnelException()
    {
    }

    public SshTunnelException(string message)
        : base(message)
    {
    }

    public SshTunnelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
