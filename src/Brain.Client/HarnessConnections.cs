using System.Diagnostics;

namespace Brain.Client;

public sealed record HarnessInfo(string Name, bool Installed, string? Executable);
public sealed record ConnectionPlan(string Harness, string Executable, string[] Arguments,
    string[] RollbackArguments, string[] VerificationArguments);
public sealed record DeviceHarnessMetadata(string[] InstalledHarnesses, string[] ConnectedHarnesses);

public interface IHarnessAdapter
{
    string Name { get; }
    Task<bool> IsInstalledAsync(CancellationToken ct);
    Task<HarnessInfo> InspectAsync(CancellationToken ct);
    Task<ConnectionPlan> PlanConnectionAsync(string brainExecutable, CancellationToken ct);
    Task ConnectAsync(ConnectionPlan plan, CancellationToken ct);
    Task DisconnectAsync(CancellationToken ct);
    Task VerifyAsync(CancellationToken ct);
}

public abstract class CommandHarnessAdapter(string name, string executableName) : IHarnessAdapter
{
    public string Name => name;
    protected string? Executable => RuntimeInventory.FindExecutable(executableName);
    public Task<bool> IsInstalledAsync(CancellationToken ct) => Task.FromResult(Executable is not null);
    public Task<HarnessInfo> InspectAsync(CancellationToken ct) =>
        Task.FromResult(new HarnessInfo(Name, Executable is not null, Executable));
    public Task<ConnectionPlan> PlanConnectionAsync(string brainExecutable, CancellationToken ct)
    {
        var executable = Executable ?? throw new InvalidOperationException($"{Name} is not installed on PATH.");
        return Task.FromResult(Plan(executable, brainExecutable));
    }
    protected abstract ConnectionPlan Plan(string executable, string brainExecutable);
    protected abstract string[] RemoveArguments { get; }
    protected abstract string[] CheckArguments { get; }

    public async Task ConnectAsync(ConnectionPlan plan, CancellationToken ct)
    {
        // Existing named connections belong to the user and are never replaced.
        var existing = await HarnessCommand.RunAsync(plan.Executable, CheckArguments, ct);
        if (existing.ExitCode == 0 && existing.Output.Contains("brain", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{Name} already has a Brain MCP entry. Inspect it before connecting.");
        var added = await HarnessCommand.RunAsync(plan.Executable, plan.Arguments, ct);
        if (added.ExitCode != 0) throw new InvalidOperationException($"{Name} rejected the connection. Inspect its MCP settings.");
        try { await VerifyAsync(ct); }
        catch
        {
            await HarnessCommand.RunAsync(plan.Executable, plan.RollbackArguments, ct);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct)
    {
        var executable = Executable ?? throw new InvalidOperationException($"{Name} is not installed on PATH.");
        var result = await HarnessCommand.RunAsync(executable, RemoveArguments, ct);
        if (result.ExitCode != 0) throw new InvalidOperationException($"{Name} could not remove Brain MCP.");
    }

    public async Task VerifyAsync(CancellationToken ct)
    {
        var executable = Executable ?? throw new InvalidOperationException($"{Name} is not installed on PATH.");
        var result = await HarnessCommand.RunAsync(executable, CheckArguments, ct);
        if (result.ExitCode != 0 || !result.Output.Contains("brain", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{Name} does not report a Brain MCP connection.");
    }
}

public sealed class ClaudeCodeAdapter() : CommandHarnessAdapter("claude-code", "claude")
{
    protected override string[] RemoveArguments => ["mcp", "remove", "--scope", "user", "brain"];
    protected override string[] CheckArguments => ["mcp", "list"];
    protected override ConnectionPlan Plan(string executable, string brainExecutable) =>
        new(Name, executable, ["mcp", "add", "--scope", "user", "brain", "--", brainExecutable, "mcp-proxy"],
            RemoveArguments, CheckArguments);
}

public sealed class CodexAdapter() : CommandHarnessAdapter("codex", "codex")
{
    protected override string[] RemoveArguments => ["mcp", "remove", "brain"];
    protected override string[] CheckArguments => ["mcp", "list"];
    protected override ConnectionPlan Plan(string executable, string brainExecutable) =>
        new(Name, executable, ["mcp", "add", "brain", "--", brainExecutable, "mcp-proxy"],
            RemoveArguments, CheckArguments);
}

public sealed class HermesAdapter() : CommandHarnessAdapter("hermes", "hermes")
{
    protected override string[] RemoveArguments => ["mcp", "remove", "brain"];
    protected override string[] CheckArguments => ["mcp", "list"];
    protected override ConnectionPlan Plan(string executable, string brainExecutable) =>
        new(Name, executable, ["mcp", "add", "brain", "--command", brainExecutable, "--args", "mcp-proxy"],
            RemoveArguments, CheckArguments);
}

public static class HarnessConnections
{
    public static IReadOnlyList<IHarnessAdapter> Adapters { get; } =
        [new ClaudeCodeAdapter(), new CodexAdapter(), new HermesAdapter()];

    public static IReadOnlyList<HarnessInfo> Inspect() => Adapters
        .Select(adapter => adapter.InspectAsync(CancellationToken.None).GetAwaiter().GetResult()).ToArray();

    public static DeviceHarnessMetadata Metadata(ClientState state) => new(
        Inspect().Where(harness => harness.Installed).Select(harness => harness.Name).ToArray(),
        state.ConnectedHarnesses);

    public static async Task ConnectAsync(string name, ClientState state, CancellationToken ct)
    {
        var adapter = Adapters.SingleOrDefault(x => x.Name == name)
            ?? throw new InvalidOperationException("Unknown harness.");
        var brain = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot find the Brain executable.");
        if (!Path.GetFileNameWithoutExtension(brain).Equals("brain", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Publish and run the standalone brain executable before connecting a harness.");
        var plan = await adapter.PlanConnectionAsync(brain, ct);
        await adapter.ConnectAsync(plan, ct);
        try { await (state with { ConnectedHarnesses = state.ConnectedHarnesses.Append(name).Distinct().ToArray() }).SaveAsync(ct); }
        catch { await adapter.DisconnectAsync(ct); throw; }
        Console.WriteLine($"{name}: Brain MCP connection registered.");
    }

    public static async Task DisconnectAsync(string name, ClientState state, CancellationToken ct)
    {
        var adapter = Adapters.SingleOrDefault(x => x.Name == name)
            ?? throw new InvalidOperationException("Unknown harness.");
        if (!state.ConnectedHarnesses.Contains(name, StringComparer.Ordinal))
            throw new InvalidOperationException("This Brain client did not register that harness connection.");
        await adapter.DisconnectAsync(ct);
        await (state with { ConnectedHarnesses = state.ConnectedHarnesses.Where(x => x != name).ToArray() }).SaveAsync(ct);
        Console.WriteLine($"{name}: Brain MCP connection removed.");
    }
}

internal sealed record CommandResult(int ExitCode, string Output);

internal static class HarnessCommand
{
    public static async Task<CommandResult> RunAsync(string executable, string[] arguments, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var process = new Process();
        process.StartInfo = new(executable) { UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = true, CreateNoWindow = true };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); throw; }
        _ = await error;
        return new(process.ExitCode, await output);
    }
}
