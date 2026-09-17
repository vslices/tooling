using ConsoleAppFramework;
using VSlices.Vsir;

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

    /// <summary>Applies one atomic semantic or metadata transition to a progressive VSIR artifact.</summary>
    /// <param name="artifact">VSIR symbol or path.</param>
    /// <param name="add">Adds members to collection-valued surfaces. The option may be repeated. Available for searchable tags metadata and semantic traits.</param>
    /// <param name="remove">Removes collection members or removable semantic assertions. The option may be repeated. Set-valued surfaces use path=value; assertion removal may use path alone.</param>
    /// <param name="set">Establishes or replaces assertions as path=value clauses. The option may be repeated; each occurrence may also contain semicolon-separated clauses. Use discovery to obtain the currently authorized paths and command templates.</param>
    public static async Task<int> Vsir(
        ConsoleAppContext context,
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
        var parseError = VsirMutationArgumentParser.AddRepeatedOptions(
            mutations,
            context.CommandArguments,
            "UPDATE020");
        if (parseError is not null)
        {
            TerminalOutput.Error(parseError);
            return 2;
        }

        if (mutations.Count == 0)
        {
            TerminalOutput.Error("UPDATE001: At least one mutation is required.");
            return 2;
        }

        var source = await File.ReadAllTextAsync(resolution.Path!, cancellationToken);
        var candidate = VsirMutationCandidate.Build(source, mutations);
        if (!candidate.IsSuccess)
        {
            TerminalOutput.Error(candidate.Error!);
            return 2;
        }

        var current = candidate.Source!;
        var validationContext = VsirValidationContext.Empty;
        var project = VSlicesProjectContext.FindFrom(resolution.Path!);
        if (project is not null)
        {
            var extensions = ProjectExtensionCatalogs.Load(project.ExtensionsRoot);
            if (!extensions.IsSuccess)
            {
                CommandInfrastructure.WriteDiagnostics(extensions.Diagnostics);
                return 2;
            }

            validationContext = extensions.Extensions!.ValidationContext;
        }

        // Projection and persistence share VsirMutationCandidate.Build. The write
        // path adds only canonical validation plus persistence after that common
        // transition, so discovery --set and update --set cannot interpret the
        // same decision into different candidates.
        var validationError = VsirMutationCandidate.Validate(current, validationContext);
        if (validationError is not null)
        {
            TerminalOutput.Error(validationError);
            return 2;
        }

        var formatted = VsirSourceFormatter.FormatAfterMutation(current);
        await CommandInfrastructure.AtomicWrite(resolution.Path!, formatted, cancellationToken);
        Console.WriteLine($"Updated '{resolution.Path}'.");
        return 0;
    }
}
