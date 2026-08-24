using System.Runtime.Versioning;
using System.Security.Principal;
using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Platform;

namespace SQLForge.Infrastructure.Windows;

/// <summary>
/// Windows での体裁。ウィンドウ装飾はモックアップどおりの自前タイトルバーを使う。
/// </summary>
public sealed class WindowsPlatformProfile : PlatformProfileBase
{
    public override PlatformKind Kind => PlatformKind.Windows;

    public override string DisplayServerName => "Win32";

    /// <summary>
    /// SQL Server の Windows 認証は、このプロセスを動かしている Windows アカウントで名乗る。
    /// 見せる名前も同じ形（<c>DOMAIN\user</c>）にしておかないと、
    /// サーバー側のユーザーやログインと突き合わせられない。
    /// </summary>
    public override string IntegratedAccountName =>
        OperatingSystem.IsWindows() ? CurrentWindowsAccountName() : base.IntegratedAccountName;

    /// <summary>Windows では OS の資格情報がそのまま渡るので、Kerberos の下ごしらえは要らない。</summary>
    public override bool IntegratedAuthenticationNeedsKerberos => false;

    /// <summary>
    /// 台帳（<see cref="PlatformProfileRegistry"/>）は実行中の OS に関わらず 3 つとも組み立てるので、
    /// Windows 以外の上で読まれても落ちないよう、呼び出し側で OS を確かめてから入る。
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static string CurrentWindowsAccountName()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return string.IsNullOrWhiteSpace(identity.Name) ? Environment.UserName : identity.Name;
        }
        catch (SystemException)
        {
            // トークンを取れないなどで名前を引けなくても、ダイアログは出せるようにしておく。
            return Environment.UserName;
        }
    }
}
