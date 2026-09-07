using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class UpdateCommands
{
    /// <summary>Updates the standalone VSlices CLI executable.</summary>
    public static Task<int> Self(
        string? channel = null,
        string? source = null,
        int? pullRequest = null,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        var project = VSlicesProjectContext.FindFrom(Environment.CurrentDirectory);
        var configuration = project?.Configuration;

        var resolvedSource = source
            ?? configuration?.UpdateSource
            ?? ProjectConfiguration.OfficialToolingSource;
        var resolvedChannel = channel
            ?? configuration?.UpdateChannel
            ?? ProjectConfiguration.DefaultUpdateChannel;
        var resolvedPullRequest = pullRequest
            ?? configuration?.UpdatePullRequest;

        TerminalOutput.Detail("Channel", resolvedChannel);
        if (resolvedChannel.Equals("build", StringComparison.OrdinalIgnoreCase) && resolvedPullRequest is not null)
            TerminalOutput.Detail("Pull request", $"#{resolvedPullRequest}");
        TerminalOutput.Detail("Mode", check ? "check only" : "install");
        TerminalOutput.BlankLine();

        return SelfUpdater.Update(
            resolvedSource,
            resolvedChannel,
            resolvedPullRequest,
            check,
            cancellationToken);
    }

    /// <summary>Updates the project-local ruleset snapshot from configured provenance.</summary>
    public static Task<int> Ruleset(CancellationToken cancellationToken = default)
    {
        var project = VSlicesProjectContext.FindFrom(Environment.CurrentDirectory);
        if (project is null)
        {
            TerminalOutput.Error(
                "UPD010: Could not locate .vslices/config.yaml. Run 'vslices init' before updating the project ruleset.");
            return Task.FromResult(1);
        }

        return RulesetUpdater.Update(project, cancellationToken);
    }

    /// <summary>Applies one atomic semantic transition to a progressive VSIR artifact.</summary>
    /// <param name="artifact">VSIR symbol or path.</param>
    /// <param name="add">Adds members to collection-valued semantic surfaces. Currently reserved for traits.</param>
    /// <param name="remove">Removes collection members or removable semantic assertions. Set-valued surfaces use path=value; assertion removal may use path alone.</param>
    /// <param name="set">Establishes or replaces semantic assertions as semicolon-separated path=value clauses. Use discovery to obtain the currently authorized paths and command templates.</param>
    public static async Task<int> Vsir(
        [Argument] string artifact,
        string? add = null,
        string? remove = null,
        string? set = null,
        CancellationToken cancellationToken = default)
    {
        var resolution = CommandInfrastructure.ResolveVsir(artifact, Environment.CurrentDirectory);
        if (resolution.Diagnostic is not null)
        {
            CommandInfrastructure.WriteDiagnostics([resolution.Diagnostic]);
            return 1;
        }

        var mutations = new List<VsirMutation>();
        var parseError = VsirMutationArgumentParser.AddMutations(mutations, VsirMutationKind.Add, add, "UPDATE020")
            ?? VsirMutationArgumentParser.AddMutations(mutations, VsirMutationKind.Remove, remove, "UPDATE020")
            ?? VsirMutationArgumentParser.AddMutations(mutations, VsirMutationKind.Set, set, "UPDATE020");
        if (parseError is not null)
        {
            TerminalOutput.Error(parseError);
            return 2;
        }

        var source = await File.ReadAllTextAsync(resolution.Path!, cancellationToken);
        var result = VsirMutationPipeline.Apply(source, mutations);
        if (!result.IsSuccess)
        {
            TerminalOutput.Error(result.Error!);
            return 2;
        }

        var formatted = VsirSourceFormatter.FormatAfterMutation(result.Source!);
        await CommandInfrastructure.AtomicWrite(resolution.Path!, formatted, cancellationToken);
        Console.WriteLine($"Updated '{resolution.Path}'.");
        return 0;
    }
}
