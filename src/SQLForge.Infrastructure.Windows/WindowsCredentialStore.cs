using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using SQLForge.Application.Abstractions;
using SQLForge.Infrastructure.Security;

namespace SQLForge.Infrastructure.Windows;

/// <summary>
/// Windows での資格情報の預け先。資格情報マネージャー（Credential Manager）の
/// 「汎用資格情報」として 1 件ずつ預ける。中身は OS が利用者のログオン資格情報で
/// 暗号化するので、SQLForge 側は鍵を持たない。
///
/// 「コントロール パネル → 資格情報マネージャー → Windows 資格情報」に
/// <c>sqlforge:&lt;接続の Id&gt;</c> という名前で並ぶ。
/// </summary>
public sealed partial class WindowsCredentialStore : PlatformSecretStore
{
    private const uint GenericCredential = 1;

    /// <summary>この機械のこの利用者にだけ残す（ドメインをまたいで持ち回らない）。</summary>
    private const uint PersistLocalMachine = 2;

    private const int NotFound = 1168;

    public override PlatformKind Kind => PlatformKind.Windows;

    public override bool IsAvailable => OperatingSystem.IsWindows();

    protected override string KeyringName => "資格情報マネージャー";

    protected override Task SaveCoreAsync(string key, string secret, CancellationToken cancellationToken)
    {
        Write(key, secret);
        return Task.CompletedTask;
    }

    protected override Task<string?> ReadCoreAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Read(key));

    protected override Task DeleteCoreAsync(string key, CancellationToken cancellationToken)
    {
        if (!CredDelete(key, GenericCredential, 0))
        {
            ThrowUnlessNotFound($"{key} を消せません。");
        }

        return Task.CompletedTask;
    }

    private static void Write(string key, string secret)
    {
        // 資格情報マネージャーの汎用資格情報は UTF-16 で読み書きするのが慣例。
        var blob = Encoding.Unicode.GetBytes(secret);
        var blobHandle = Marshal.AllocHGlobal(blob.Length == 0 ? 1 : blob.Length);
        var targetName = Marshal.StringToHGlobalUni(key);
        var userName = Marshal.StringToHGlobalUni(key);

        try
        {
            Marshal.Copy(blob, 0, blobHandle, blob.Length);

            var credential = new Credential
            {
                Type = GenericCredential,
                TargetName = targetName,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobHandle,
                Persist = PersistLocalMachine,
                UserName = userName
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"{key} を資格情報マネージャーへ預けられません。");
            }
        }
        finally
        {
            // 預けたあとの控えは残さない。
            Array.Clear(blob);
            Fill(blobHandle, blob.Length, 0);
            Marshal.FreeHGlobal(blobHandle);
            Marshal.ZeroFreeGlobalAllocUnicode(targetName);
            Marshal.ZeroFreeGlobalAllocUnicode(userName);
        }
    }

    private static string? Read(string key)
    {
        if (!CredRead(key, GenericCredential, 0, out var handle))
        {
            ThrowUnlessNotFound($"{key} を資格情報マネージャーから読めません。");
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(handle);
            if (credential.CredentialBlobSize == 0)
            {
                return string.Empty;
            }

            var blob = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            var secret = Encoding.Unicode.GetString(blob);
            Array.Clear(blob);

            return secret;
        }
        finally
        {
            CredFree(handle);
        }
    }

    /// <summary>「無い」は想定内なので黙って通し、それ以外の失敗だけを投げる。</summary>
    private static void ThrowUnlessNotFound(string message)
    {
        var error = Marshal.GetLastWin32Error();
        if (error != NotFound)
        {
            throw new Win32Exception(error, message);
        }
    }

    private static void Fill(nint buffer, int length, byte value)
    {
        for (var offset = 0; offset < length; offset++)
        {
            Marshal.WriteByte(buffer, offset, value);
        }
    }

    /// <summary>advapi32 の CREDENTIALW。使う欄だけを埋め、残りは 0 のままにする。</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public nint TargetName;
        public nint Comment;
        // FILETIME は 32 ビット 2 つ。読み書きしないので、そのぶんの場所だけ空けておく。
        public uint LastWrittenLow;
        public uint LastWrittenHigh;
        public uint CredentialBlobSize;
        public nint CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public nint Attributes;
        public nint TargetAlias;
        public nint UserName;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredWrite(ref Credential credential, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredReadW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredRead(string targetName, uint type, uint reservedFlag, out nint credential);

    [LibraryImport("advapi32.dll", EntryPoint = "CredDeleteW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CredDelete(string targetName, uint type, uint flags);

    [LibraryImport("advapi32.dll", EntryPoint = "CredFree")]
    private static partial void CredFree(nint buffer);
}
