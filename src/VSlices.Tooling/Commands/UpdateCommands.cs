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

    /// <summary>Updates the project-local Docs Standard snapshot.</summary>
    /// <param name="from">Docs Standard source directory, GitHub repository, or ZIP URL. Defaults to the official source.</param>
    /// <param name="ref">GitHub branch, tag, or commit. Defaults to main for the official source.</param>
    public static Task<int> DocsStandard(
        string? from = null,
        string? @ref = null,
        CancellationToken cancellationToken = default)
    {
        var project = VSlicesProjectContext.FindFrom(Environment.CurrentDirectory);
        if (project is null)
        {
            TerminalOutput.Error(
                "UPD030: Could not locate .vslices/config.yaml. Run 'vslices init' before updating Docs Standard.");
            return Task.FromResult(1);
        }

        var configuration = project.Configuration;
        var explicitSource = !string.IsNullOrWhiteSpace(from);
        var source = explicitSource
            ? from!
            : configuration.DocsStandardSource
              ?? DocsStandardUpdater.OfficialSource;

        string? reference;
        if (!string.IsNullOrWhiteSpace(@ref))
        {
            reference = @ref;
        }
        else if (explicitSource)
        {
            reference = source.Equals(
                DocsStandardUpdater.OfficialSource,
                StringComparison.OrdinalIgnoreCase)
                ? DocsStandardUpdater.OfficialRef
                : null;
        }
        else
        {
            reference = configuration.DocsStandardRef;
            if (string.IsNullOrWhiteSpace(reference) &&
                source.Equals(
                    DocsStandardUpdater.OfficialSource,
                    StringComparison.OrdinalIgnoreCase))
            {
                reference = DocsStandardUpdater.OfficialRef;
            }
        }

        return DocsStandardUpdater.Update(project, source, reference, cancellationToken);
    }

    /// <summary>Answers or replaces one question on the current valid Document authoring surface.</summary>
    /// <param name="document">Document name or path. The .md extension is added when omitted.</param>
    /// <param name="questionId">Ephemeral 1-based selection from the current Document authoring surface.</param>
    /// <param name="answer">Non-empty Markdown answer for the selected question.</param>
    public static async Task<int> Document(
        [Argument] string document,
        int? questionId = null,
        string? answer = null,
        CancellationToken cancellationToken = default)
    {
        if (questionId is null || questionId <= 0)
        {
            TerminalOutput.Error("UPDATE100: --question-id <number> is required and must be greater than zero.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(answer))
        {
            TerminalOutput.Error("UPDATE101: --answer must contain non-whitespace text.");
            return 2;
        }

        var path = Path.GetFullPath(
            document.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? document
                : document + ".md",
            Environment.CurrentDirectory);
        if (!File.Exists(path))
        {
            TerminalOutput.Error($"UPDATE102: Document '{path}' does not exist.");
            return 1;
        }

        var standardRoot = DocsStandardCatalog.FindInstalledRoot(path);
        if (standardRoot is null)
        {
            TerminalOutput.Error(
                "UPDATE103: Could not locate an installed Docs Standard snapshot at .vslices/docs-standard.");
            return 1;
        }

        var catalog = DocsStandardCatalog.Load(standardRoot);
        if (!catalog.IsSuccess)
        {
            TerminalOutput.Error(catalog.Error!);
            return 1;
        }

        var source = await File.ReadAllTextAsync(path, cancellationToken);
        var state = DocumentArtifact.Read(source, catalog.Catalog!);
        if (!state.IsSuccess)
        {
            TerminalOutput.Error(state.Error!);
            return 2;
        }

        var candidate = state.Artifact!.Update(questionId.Value, answer);
        if (!candidate.IsSuccess)
        {
            TerminalOutput.Error(candidate.Error!);
            return 2;
        }

        await CommandInfrastructure.AtomicWrite(path, candidate.Source!, cancellationToken);
        Console.WriteLine(
            $"Updated question [{questionId.Value}] '{candidate.Question!.Text}' in '{path}'.");
        return 0;
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
