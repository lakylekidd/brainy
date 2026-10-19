using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Brain.Client;

public sealed record ClientState(Uri Server, Guid DeviceId, string Name)
{
    public string[] ConnectedHarnesses { get; init; } = [];
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".brain");
    public static string FilePath => Path.Combine(DirectoryPath, "client.json");

    public static async Task<ClientState?> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(FilePath)) return null;
        return JsonSerializer.Deserialize<ClientState>(await File.ReadAllTextAsync(FilePath, ct));
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(this), ct);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(temporary, FilePath, true);
    }
}

public static class SecureCredentialStore
{
    private static string Service => "homelab-brain-client";

    public static async Task SaveAsync(string credential, CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(ClientState.DirectoryPath);
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(credential), null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(Path.Combine(ClientState.DirectoryPath, "credential.bin"), bytes, ct);
            return;
        }
        if (OperatingSystem.IsLinux())
        {
            await RunSecretToolAsync(["store", "--label=Homelab Brain Client", "application", Service], credential, ct);
            return;
        }
        if (OperatingSystem.IsMacOS()) { MacKeychain.Save(Service, credential); return; }
        throw new PlatformNotSupportedException("Secure credential storage is unavailable on this platform.");
    }

    public static async Task<string?> LoadAsync(CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows())
        {
            var path = Path.Combine(ClientState.DirectoryPath, "credential.bin");
            return File.Exists(path) ? Encoding.UTF8.GetString(ProtectedData.Unprotect(
                await File.ReadAllBytesAsync(path, ct), null, DataProtectionScope.CurrentUser)) : null;
        }
        if (OperatingSystem.IsLinux())
        {
            var result = await RunSecretToolAsync(["lookup", "application", Service], null, ct, false);
            return result?.TrimEnd('\r', '\n');
        }
        if (OperatingSystem.IsMacOS()) return MacKeychain.Load(Service);
        return null;
    }

    public static async Task DeleteAsync(CancellationToken ct = default)
    {
        if (OperatingSystem.IsWindows())
        {
            var path = Path.Combine(ClientState.DirectoryPath, "credential.bin");
            if (File.Exists(path)) File.Delete(path);
        }
        else if (OperatingSystem.IsLinux())
            await RunSecretToolAsync(["clear", "application", Service], null, ct, false);
        else if (OperatingSystem.IsMacOS()) MacKeychain.Delete(Service);
    }

    private static async Task<string?> RunSecretToolAsync(string[] arguments, string? input,
        CancellationToken ct, bool required = true)
    {
        using var process = new System.Diagnostics.Process();
        process.StartInfo = new("secret-tool") { RedirectStandardInput = input is not null,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        try { process.Start(); }
        catch (System.ComponentModel.Win32Exception)
        { throw new InvalidOperationException("Install libsecret-tools and unlock a Secret Service keyring before login."); }
        if (input is not null) { await process.StandardInput.WriteAsync(input.AsMemory(), ct); process.StandardInput.Close(); }
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0 && required) throw new InvalidOperationException("The OS credential store rejected the request.");
        return process.ExitCode == 0 ? output : null;
    }
}

internal static class MacKeychain
{
    private const string Library = "/System/Library/Frameworks/Security.framework/Security";
    private static readonly byte[] Account = Encoding.UTF8.GetBytes("brain-client");

    public static void Save(string service, string credential)
    {
        Delete(service);
        var serviceBytes = Encoding.UTF8.GetBytes(service);
        var value = Encoding.UTF8.GetBytes(credential);
        var status = Add(IntPtr.Zero, (uint)serviceBytes.Length, serviceBytes,
            (uint)Account.Length, Account, (uint)value.Length, value, out _);
        if (status != 0) throw new InvalidOperationException($"macOS Keychain rejected the credential ({status}).");
    }

    public static string? Load(string service)
    {
        var serviceBytes = Encoding.UTF8.GetBytes(service);
        var status = Find(IntPtr.Zero, (uint)serviceBytes.Length, serviceBytes,
            (uint)Account.Length, Account, out var length, out var data, out _);
        if (status == -25300) return null;
        if (status != 0) throw new InvalidOperationException($"macOS Keychain lookup failed ({status}).");
        try
        {
            var bytes = new byte[length];
            Marshal.Copy(data, bytes, 0, (int)length);
            return Encoding.UTF8.GetString(bytes);
        }
        finally { FreeContent(IntPtr.Zero, data); }
    }

    public static void Delete(string service)
    {
        var serviceBytes = Encoding.UTF8.GetBytes(service);
        var status = Find(IntPtr.Zero, (uint)serviceBytes.Length, serviceBytes,
            (uint)Account.Length, Account, out _, out var data, out var item);
        if (status == -25300) return;
        if (status != 0) throw new InvalidOperationException($"macOS Keychain lookup failed ({status}).");
        try { if (DeleteItem(item) != 0) throw new InvalidOperationException("macOS Keychain deletion failed."); }
        finally { FreeContent(IntPtr.Zero, data); }
    }

    [System.Runtime.InteropServices.DllImport(Library, EntryPoint = "SecKeychainAddGenericPassword")]
    private static extern int Add(IntPtr keychain, uint serviceLength, byte[] service, uint accountLength,
        byte[] account, uint passwordLength, byte[] password, out IntPtr item);
    [System.Runtime.InteropServices.DllImport(Library, EntryPoint = "SecKeychainFindGenericPassword")]
    private static extern int Find(IntPtr keychain, uint serviceLength, byte[] service, uint accountLength,
        byte[] account, out uint passwordLength, out IntPtr passwordData, out IntPtr item);
    [System.Runtime.InteropServices.DllImport(Library, EntryPoint = "SecKeychainItemFreeContent")]
    private static extern int FreeContent(IntPtr attributes, IntPtr data);
    [System.Runtime.InteropServices.DllImport(Library, EntryPoint = "SecKeychainItemDelete")]
    private static extern int DeleteItem(IntPtr item);
}
