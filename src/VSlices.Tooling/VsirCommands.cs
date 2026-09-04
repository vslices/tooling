using ConsoleAppFramework;
using VSlices.Targets.DotNet;
using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Tooling;

internal static class VsirCommands
{
    /// <summary>Produces a deterministic projection from a VSIR artifact.</summary>
    /// <param name="subject">VSIR symbol or path. The .vsir extension is optional.</param>
    /// <param name="to">-to, Optional target language. Inferred when exactly one supported target is installed.</param>
    /// <param name="output">-o, Optional output path. By default a sibling materialization is written.</param>
    /// <param name="stdout">Write the result to standard output instead of a file. Equivalent to -o -.</param>
    /// <param name="force">Replace an existing output explicitly.</param>
    /// <param name="namespace">-n, Optional namespace override. By default VSlices asks the .NET target tooling for project/item context.</param>
    public static async Task<int> Transpile(
        [Argument] string subject,
        string? to = null,
        string? output = null,
        bool stdout = false,
        bool force = false,
        string? @namespace = null,
        CancellationToken cancellationToken = default)
    {
        var lowered = await TranspileCore(subject, to, @namespace, cancellationToken);
        if (!lowered.IsSuccess)
        {
            CommandInfrastructure.WriteDiagnostics(lowered.Diagnostics);
            return 1;
        }

        return await WriteLoweredResult(
            lowered,
            output,
            stdout,
            overwrite: force,
            cancellationToken);
    }

    /// <summary>Re-applies a deterministic VSIR change over a human-edited projection.</summary>
    /// <param name="subject">New VSIR symbol or path.</param>
    /// <param name="to">-to, Optional target language. Inferred when exactly one supported target is installed.</param>
    /// <param name="from">Previous VSIR symbol or path used to reconstruct the deterministic baseline.</param>
    /// <param name="source">Human-edited projection to preserve. By default the sibling materialization is used.</param>
    /// <param name="output">-o, Optional output path. By default the source materialization is updated.</param>
    /// <param name="stdout">Write the result to standard output instead of updating a file. Equivalent to -o -.</param>
    /// <param name="namespace">-n, Optional namespace override. By default VSlices asks the target tooling for project/item context.</param>
    public static async Task<int> Rebase(
        [Argument] string subject,
        string? to = null,
        string? from = null,
        string? source = null,
        string? output = null,
        bool stdout = false,
        string? @namespace = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(from))
        {
            Console.Error.WriteLine(
                "CLI040: Rebase needs the previous VSIR baseline. Specify --from <previous-vsir>. Automatic provenance is currently orchestrated by 'vslices lower'.");
            return 2;
        }

        var rebased = await RebaseCore(
            subject,
            to,
            from,
            source,
            @namespace,
            cancellationToken);

        if (!rebased.IsSuccess)
        {
            CommandInfrastructure.WriteDiagnostics(rebased.Diagnostics);
            return 1;
        }

        return await CommandInfrastructure.WriteResult(
            rebased.Source!,
            rebased.SourcePath!,
            output,
            stdout,
            overwrite: true,
            cancellationToken);
    }

    /// <summary>Lowers a VSIR artifact using the least powerful currently available mechanism.</summary>
    /// <param name="subject">VSIR symbol or path.</param>
    /// <param name="to">-to, Optional target language. Inferred when exactly one supported target is installed.</param>
    /// <param name="from">Previous VSIR baseline override when automatic lineage cannot be established.</param>
    /// <param name="source">Existing human-edited materialization override.</param>
    /// <param name="output">-o, Optional output path.</param>
    /// <param name="stdout">Write the result to standard output instead of a file. Equivalent to -o -.</param>
    /// <param name="namespace">-n, Optional namespace override.</param>
    public static async Task<int> Lower(
        [Argument] string subject,
        string? to = null,
        string? from = null,
        string? source = null,
        string? output = null,
        bool stdout = false,
        string? @namespace = null,
        CancellationToken cancellationToken = default)
    {
        var resolution = CommandInfrastructure.ResolveVsir(subject, Environment.CurrentDirectory);
        if (resolution.Diagnostic is not null)
        {
            CommandInfrastructure.WriteDiagnostics([resolution.Diagnostic]);
            return 1;
        }

        var rulesetRoot = RulesetLocator.FindFrom(resolution.Path!);
        if (rulesetRoot is null)
        {
            CommandInfrastructure.WriteDiagnostics([new(
                "CLI010",
                "No project-local VSlices ruleset was found. Expected .vslices/ruleset/manifest.yaml in the VSIR path ancestry. Run 'vslices init'.")]);
            return 1;
        }

        var target = CommandInfrastructure.ResolveTarget(to, rulesetRoot);
        if (target.Diagnostic is not null)
        {
            CommandInfrastructure.WriteDiagnostics([target.Diagnostic]);
            return 1;
        }

        var conventional = CommandInfrastructure.ConventionalMaterializationPath(
            resolution.Path!,
            target.Target!);

        var existing = string.IsNullOrWhiteSpace(source)
            ? conventional
            : Path.GetFullPath(source, Environment.CurrentDirectory);

        if (!File.Exists(existing))
        {
            var lowered = await TranspileCore(
                subject,
                CommandInfrastructure.DisplayTarget(target.Target!),
                @namespace,
                cancellationToken);

            if (!lowered.IsSuccess)
            {
                CommandInfrastructure.WriteDiagnostics(lowered.Diagnostics);
                return 1;
            }

            var exitCode = await WriteLoweredResult(
                lowered,
                output,
                stdout,
                overwrite: false,
                cancellationToken);

            if (exitCode == 0 && TryResolveWrittenPath(conventional, output, stdout, out var writtenPath))
            {
                await LoweringLineageStore.TryWrite(
                    rulesetRoot,
                    writtenPath!,
                    target.Target!,
                    lowered.Source!,
                    cancellationToken);
            }

            return exitCode;
        }

        RebaseCommandResult rebased;

        if (!string.IsNullOrWhiteSpace(from))
        {
            rebased = await RebaseCore(
                subject,
                CommandInfrastructure.DisplayTarget(target.Target!),
                from,
                existing,
                @namespace,
                cancellationToken);
        }
        else
        {
            var next = await TranspileCore(
                subject,
                CommandInfrastructure.DisplayTarget(target.Target!),
                @namespace,
                cancellationToken);

            if (!next.IsSuccess)
            {
                CommandInfrastructure.WriteDiagnostics(next.Diagnostics);
                return 1;
            }

            var previousDeterministic = await LoweringLineageStore.TryRead(
                rulesetRoot,
                existing,
                target.Target!,
                cancellationToken);

            if (previousDeterministic is null)
            {
                var human = await File.ReadAllTextAsync(existing, cancellationToken);
                if (string.Equals(human, next.Source, StringComparison.Ordinal))
                {
                    await LoweringLineageStore.TryWrite(
                        rulesetRoot,
                        existing,
                        target.Target!,
                        next.Source!,
                        cancellationToken);

                    Console.WriteLine($"Established lowering lineage for '{existing}'.");
                    return 0;
                }

                if (!LoweringLineageBootstrap.IsConfiguredFor(
                        rulesetRoot,
                        conventional,
                        existing,
                        source))
                {
                    Console.Error.WriteLine(
                        "LOWER001: No trustworthy deterministic baseline could be inferred. Configure lineage.bootstrap.convention for the conventional materialization, or run once with --from <previous-vsir> to establish lineage explicitly.");
                    return 1;
                }

                await LoweringLineageStore.TryWrite(
                    rulesetRoot,
                    existing,
                    target.Target!,
                    next.Source!,
                    cancellationToken);

                TerminalOutput.Detail(
                    "Lineage bootstrap",
                    ProjectConfiguration.DefaultLineageBootstrapConvention);
                TerminalOutput.Success("✓ Lowering lineage established without modifying the existing materialization");
                return 0;
            }

            rebased = await RebaseFromDeterministicBaseline(
                next,
                previousDeterministic,
                existing,
                cancellationToken);
        }

        if (!rebased.IsSuccess)
        {
            CommandInfrastructure.WriteDiagnostics(rebased.Diagnostics);
            return 1;
        }

        var writeExitCode = await CommandInfrastructure.WriteResult(
            rebased.Source!,
            rebased.SourcePath!,
            output,
            stdout,
            overwrite: true,
            cancellationToken);

        if (writeExitCode == 0 &&
            TryResolveWrittenPath(rebased.SourcePath!, output, stdout, out var rebasedPath))
        {
            await LoweringLineageStore.TryWrite(
                rulesetRoot,
                rebasedPath!,
                target.Target!,
                rebased.DeterministicSource!,
                cancellationToken);
        }

        return writeExitCode;
    }

    private static async Task<int> WriteLoweredResult(
        LoweringCommandResult lowered,
        string? output,
        bool stdout,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var defaultPath = CommandInfrastructure.ConventionalMaterializationPath(
            lowered.VsirPath!,
            lowered.Target!);

        return await CommandInfrastructure.WriteResult(
            lowered.Source!,
            defaultPath,
            output,
            stdout,
            overwrite,
            cancellationToken);
    }

    private static async Task<RebaseCommandResult> RebaseCore(
        string subject,
        string? to,
        string from,
        string? source,
        string? namespaceOverride,
        CancellationToken cancellationToken)
    {
        var next = await TranspileCore(subject, to, namespaceOverride, cancellationToken);
        if (!next.IsSuccess)
            return RebaseCommandResult.Failure(next.Diagnostics);

        var previous = await TranspileCore(
            from,
            CommandInfrastructure.DisplayTarget(next.Target!),
            namespaceOverride,
            cancellationToken);

        if (!previous.IsSuccess)
            return RebaseCommandResult.Failure(previous.Diagnostics);

        var conventionalSource = CommandInfrastructure.ConventionalMaterializationPath(
            next.VsirPath!,
            next.Target!);

        var resolvedHuman = string.IsNullOrWhiteSpace(source)
            ? conventionalSource
            : Path.GetFullPath(source, Environment.CurrentDirectory);

        return await RebaseFromDeterministicBaseline(
            next,
            previous.Source!,
            resolvedHuman,
            cancellationToken);
    }

    private static async Task<RebaseCommandResult> RebaseFromDeterministicBaseline(
        LoweringCommandResult next,
        string previousDeterministic,
        string humanPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(humanPath))
        {
            return RebaseCommandResult.Failure([new(
                "CLI003",
                $"Could not resolve human projection '{humanPath}'.")]);
        }

        var human = await File.ReadAllTextAsync(humanPath, cancellationToken);
        var rebased = CSharpRebaser.Rebase(previousDeterministic, human, next.Source!);

        return rebased.IsSuccess
            ? RebaseCommandResult.Success(rebased.Source!, humanPath, next.Source!)
            : RebaseCommandResult.Failure(rebased.Diagnostics);
    }

    private static bool TryResolveWrittenPath(
        string defaultPath,
        string? output,
        bool stdout,
        out string? resolved)
    {
        if (stdout || output == "-")
        {
            resolved = null;
            return false;
        }

        resolved = string.IsNullOrWhiteSpace(output)
            ? defaultPath
            : Path.GetFullPath(output, Environment.CurrentDirectory);
        return true;
    }

    private static async Task<LoweringCommandResult> TranspileCore(
        string input,
        string? requestedTarget,
        string? namespaceOverride,
        CancellationToken cancellationToken)
    {
        var resolution = CommandInfrastructure.ResolveVsir(input, Environment.CurrentDirectory);
        if (resolution.Diagnostic is not null)
            return LoweringCommandResult.Failure([resolution.Diagnostic]);

        var rulesetRoot = RulesetLocator.FindFrom(resolution.Path!);
        if (rulesetRoot is null)
        {
            return LoweringCommandResult.Failure([new(
                "CLI010",
                "No project-local VSlices ruleset was found. Expected .vslices/ruleset/manifest.yaml in the VSIR path ancestry. Run 'vslices init'.")]);
        }

        var target = CommandInfrastructure.ResolveTarget(requestedTarget, rulesetRoot);
        if (target.Diagnostic is not null)
            return LoweringCommandResult.Failure([target.Diagnostic]);

        if (target.Target != "csharp")
        {
            return LoweringCommandResult.Failure([new(
                "CLI020",
                $"Target '{target.Target}' is not supported by the current lowering engine.")]);
        }

        var rules = CSharpLoweringRuleSet.Load(rulesetRoot);
        if (!rules.IsSuccess)
            return LoweringCommandResult.Failure(rules.Diagnostics);

        var targetContext = await DotNetTargetContextResolver.Resolve(
            resolution.Path!,
            namespaceOverride,
            cancellationToken);

        if (targetContext.Diagnostic is not null)
            return LoweringCommandResult.Failure([targetContext.Diagnostic]);

        var text = await File.ReadAllTextAsync(resolution.Path!, cancellationToken);
        var parsed = VsirParser.Parse(text);
        if (!parsed.IsSuccess)
            return LoweringCommandResult.Failure(parsed.Diagnostics);

        var lowered = CSharpLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext(
                targetContext.Context!.Namespace,
                rules.RuleSet!));

        return lowered.IsSuccess
            ? LoweringCommandResult.Success(lowered.Source!, resolution.Path!, target.Target!)
            : LoweringCommandResult.Failure(lowered.Diagnostics);
    }

    private sealed record LoweringCommandResult(
        string? Source,
        string? VsirPath,
        string? Target,
        IReadOnlyList<VsirDiagnostic> Diagnostics)
    {
        public bool IsSuccess => Source is not null && Diagnostics.Count == 0;

        public static LoweringCommandResult Success(string source, string path, string target) =>
            new(source, path, target, []);

        public static LoweringCommandResult Failure(IEnumerable<VsirDiagnostic> diagnostics) =>
            new(null, null, null, diagnostics.ToArray());
    }

    private sealed record RebaseCommandResult(
        string? Source,
        string? SourcePath,
        string? DeterministicSource,
        IReadOnlyList<VsirDiagnostic> Diagnostics)
    {
        public bool IsSuccess =>
            Source is not null &&
            SourcePath is not null &&
            DeterministicSource is not null &&
            Diagnostics.Count == 0;

        public static RebaseCommandResult Success(
            string source,
            string sourcePath,
            string deterministicSource) =>
            new(source, sourcePath, deterministicSource, []);

        public static RebaseCommandResult Failure(IEnumerable<VsirDiagnostic> diagnostics) =>
            new(null, null, null, diagnostics.ToArray());
    }
}
