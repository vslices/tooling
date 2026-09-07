using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class UpdateCommands
{
    /// <summary>Updates VSlices tooling components using the legacy flag-oriented surface.</summary>
    public static Task<int> Update(
        bool self = false,
        bool ruleset = false,
        string? channel = null,
        string? source = null,
        int? pullRequest = null,
        bool check = false,
        CancellationToken cancellationToken = default)
    {
        if (!self && !ruleset)
        {
            TerminalOutput.Error(
                "UPD000: Specify what to update. Prefer the subject-oriented surfaces 'vslices update self' or 'vslices update ruleset'.");
            return Task.FromResult(2);
        }

        if (self && ruleset)
        {
            TerminalOutput.Error(
                "UPD001: Updating self and ruleset together is not defined. Run the explicit update subjects separately.");
            return Task.FromResult(2);
        }

        return self
            ? Self(channel, source, pullRequest, check, cancellationToken)
            : Ruleset(cancellationToken);
    }

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
    /// <param name="addTags">Comma-separated tags to add.</param>
    /// <param name="removeTags">Comma-separated tags to remove.</param>
    /// <param name="setTags">Comma-separated replacement tag set.</param>
    /// <param name="add">Generic add mutations as semicolon-separated path=value clauses.</param>
    /// <param name="remove">Generic remove mutations as semicolon-separated path=value clauses.</param>
    /// <param name="set">Generic set mutations as semicolon-separated path=value clauses.</param>
    public static async Task<int> Vsir(
        [Argument] string artifact,
        string? addTags = null,
        string? removeTags = null,
        string? setTags = null,
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
        if (addTags is not null)
            mutations.Add(new(VsirMutationKind.Add, "tags", addTags));
        if (removeTags is not null)
            mutations.Add(new(VsirMutationKind.Remove, "tags", removeTags));
        if (setTags is not null)
            mutations.Add(new(VsirMutationKind.Set, "tags", setTags));

        var parseError = AddGenericMutations(mutations, VsirMutationKind.Add, add)
            ?? AddGenericMutations(mutations, VsirMutationKind.Remove, remove)
            ?? AddGenericMutations(mutations, VsirMutationKind.Set, set);
        if (parseError is not null)
        {
            TerminalOutput.Error(parseError);
            return 2;
        }

        var source = await File.ReadAllTextAsync(resolution.Path!, cancellationToken);
        var result = VsirMutationEngine.Apply(source, mutations);
        if (!result.IsSuccess)
        {
            TerminalOutput.Error(result.Error!);
            return 2;
        }

        await CommandInfrastructure.AtomicWrite(resolution.Path!, result.Source!, cancellationToken);
        Console.WriteLine($"Updated '{resolution.Path}'.");
        return 0;
    }

    internal static string? AddGenericMutations(
        ICollection<VsirMutation> target,
        VsirMutationKind kind,
        string? clauses)
    {
        if (string.IsNullOrWhiteSpace(clauses))
            return null;

        foreach (var clause in clauses.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = clause.IndexOf('=');
            if (separator <= 0 || separator == clause.Length - 1)
            {
                return $"UPDATE020: Mutation '{clause}' must use path=value syntax.";
            }

            var path = clause[..separator].Trim();
            var value = clause[(separator + 1)..].Trim();
            if (path.Length == 0 || value.Length == 0)
                return $"UPDATE020: Mutation '{clause}' must use non-empty path=value syntax.";

            target.Add(new(kind, path, value));
        }

        return null;
    }
}
