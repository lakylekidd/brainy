using System.Diagnostics;
using System.Security;

namespace Brain.Client;

public static class ServiceInstaller
{
    public static async Task InstallAsync(CancellationToken ct)
    {
        if (await ClientState.LoadAsync(ct) is null)
            throw new InvalidOperationException("Run brain login before installing the Runtime Agent.");
        if (await SecureCredentialStore.LoadAsync(ct) is null)
            throw new InvalidOperationException("The Runtime Agent credential is unavailable to this user.");
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate brain executable.");
        if (!Path.GetFileNameWithoutExtension(executable).Equals("brain", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Publish and run the standalone brain executable before service installation.");
        if (OperatingSystem.IsLinux())
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "systemd", "user");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "brain-runtime-agent.service");
            if (File.Exists(path)) throw new InvalidOperationException("Runtime Agent service already exists.");
            await File.WriteAllTextAsync(path, SystemdUnit(executable), ct);
            await RunAsync("systemctl", ["--user", "daemon-reload"], ct);
            await RunAsync("systemctl", ["--user", "enable", "--now", "brain-runtime-agent.service"], ct);
            Console.WriteLine("Runtime Agent systemd user service installed.");
        }
        else if (OperatingSystem.IsMacOS())
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "LaunchAgents");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "com.homelab.brain-runtime-agent.plist");
            if (File.Exists(path)) throw new InvalidOperationException("Runtime Agent LaunchAgent already exists.");
            await File.WriteAllTextAsync(path, LaunchdPlist(executable), ct);
            await RunAsync("launchctl", ["load", "-w", path], ct);
            Console.WriteLine("Runtime Agent launchd user service installed.");
        }
        else if (OperatingSystem.IsWindows())
            throw new InvalidOperationException("Windows Service setup requires a dedicated service identity and credential-store access. Run brain agent run under that identity until service provisioning is configured.");
        else throw new PlatformNotSupportedException();
    }

    public static async Task UninstallAsync(CancellationToken ct)
    {
        if (OperatingSystem.IsLinux())
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "systemd", "user", "brain-runtime-agent.service");
            if (!File.Exists(path)) return;
            if (!(await File.ReadAllTextAsync(path, ct)).Contains("Description=Homelab Brain Runtime Agent", StringComparison.Ordinal))
                throw new InvalidOperationException("Service file is not a Brain Runtime Agent unit.");
            await RunAsync("systemctl", ["--user", "disable", "--now", "brain-runtime-agent.service"], ct);
            File.Delete(path);
            await RunAsync("systemctl", ["--user", "daemon-reload"], ct);
        }
        else if (OperatingSystem.IsMacOS())
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "LaunchAgents", "com.homelab.brain-runtime-agent.plist");
            if (!File.Exists(path)) return;
            if (!(await File.ReadAllTextAsync(path, ct)).Contains("com.homelab.brain-runtime-agent", StringComparison.Ordinal))
                throw new InvalidOperationException("LaunchAgent file is not a Brain Runtime Agent unit.");
            await RunAsync("launchctl", ["unload", "-w", path], ct);
            File.Delete(path);
        }
        else if (OperatingSystem.IsWindows())
            throw new InvalidOperationException("Windows Service installation is not managed by this client.");
        Console.WriteLine("Runtime Agent service removed.");
    }

    public static string SystemdUnit(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Any(c => c is '\n' or '\r' or '\0'))
            throw new ArgumentException("Invalid executable path.", nameof(executable));
        var escaped = executable.Replace("%", "%%").Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"[Unit]\nDescription=Homelab Brain Runtime Agent\nAfter=network-online.target\n\n" +
            $"[Service]\nType=simple\nExecStart=\"{escaped}\" agent run\nRestart=on-failure\nRestartSec=30\nNoNewPrivileges=true\nPrivateTmp=true\n\n" +
            "[Install]\nWantedBy=default.target\n";
    }

    public static string LaunchdPlist(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Any(c => c is '\n' or '\r' or '\0'))
            throw new ArgumentException("Invalid executable path.", nameof(executable));
        var escaped = SecurityElement.Escape(executable);
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            $"<plist version=\"1.0\"><dict><key>Label</key><string>com.homelab.brain-runtime-agent</string>" +
            $"<key>ProgramArguments</key><array><string>{escaped}</string><string>agent</string><string>run</string></array>" +
            "<key>RunAtLoad</key><true/><key>KeepAlive</key><true/></dict></plist>\n";
    }

    private static async Task RunAsync(string executable, string[] arguments, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new(executable) { UseShellExecute = false, RedirectStandardError = true,
            RedirectStandardOutput = true, CreateNoWindow = true };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        _ = await output; _ = await error;
        if (process.ExitCode != 0) throw new InvalidOperationException("The OS service manager rejected Runtime Agent installation.");
    }
}
