using VSlices.Targets.DotNet;
using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Tooling;

internal static class TranspilationOperation
{
    public static async Task<TranspilationResult> Execute(
        string subject,
        string? requestedTarget,
        string? namespaceOverride,
        CancellationToken cancellationToken)
    {
        var resolution = CommandInfrastructure.ResolveVsir(subject, Environment.CurrentDirectory);
        if (resolution.Diagnostic is not null)
            return TranspilationResult.Failure([resolution.Diagnostic]);

        var project = VSlicesProjectContext.FindFrom(resolution.Path!);
        if (project is null || !File.Exists(Path.Combine(project.RulesetRoot, "manifest.yaml")))
        {
            return TranspilationResult.Failure([new(
                "CLI010",
                "No project-local VSlices project/ruleset was found. Expected .vslices/config.yaml and .vslices/ruleset/manifest.yaml in the VSIR path ancestry. Run 'vslices init'.")]);
        }

        var target = CommandInfrastructure.ResolveTarget(requestedTarget, project);
        if (target.Diagnostic is not null)
            return TranspilationResult.Failure([target.Diagnostic]);

        if (target.Target != "csharp")
        {
            return TranspilationResult.Failure([new(
                "CLI020",
                $"Target '{target.Target}' is not supported by the current lowering engine.")]);
        }

        var rules = CSharpLoweringRuleSet.Load(project.RulesetRoot);
        if (!rules.IsSuccess)
            return TranspilationResult.Failure(rules.Diagnostics);

        var targetContext = await DotNetTargetContextResolver.Resolve(
            resolution.Path!,
            namespaceOverride,
            cancellationToken,
            project.Configuration.CSharpNamespaceIgnoredFolders);

        if (targetContext.Diagnostic is not null)
            return TranspilationResult.Failure([targetContext.Diagnostic]);

        var text = await File.ReadAllTextAsync(resolution.Path!, cancellationToken);
        var parsed = VsirParser.Parse(text);
        if (!parsed.IsSuccess)
            return TranspilationResult.Failure(parsed.Diagnostics);

        var lowered = CSharpLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext(
                targetContext.Context!.Namespace,
                rules.RuleSet!));

        return lowered.IsSuccess
            ? TranspilationResult.Success(
                lowered.Source!,
                resolution.Path!,
                parsed.Document!.Name,
                target.Target!,
                project,
                targetContext.Context!)
            : TranspilationResult.Failure(lowered.Diagnostics);
    }
}

internal sealed record TranspilationResult(
    string? Source,
    string? VsirPath,
    string? SemanticName,
    string? Target,
    VSlicesProjectContext? Project,
    DotNetTargetContext? TargetContext,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess =>
        Source is not null &&
        VsirPath is not null &&
        SemanticName is not null &&
        Target is not null &&
        Project is not null &&
        TargetContext is not null &&
        Diagnostics.Count == 0;

    public static TranspilationResult Success(
        string source,
        string path,
        string semanticName,
        string target,
        VSlicesProjectContext project,
        DotNetTargetContext targetContext) =>
        new(source, path, semanticName, target, project, targetContext, []);

    public static TranspilationResult Failure(IEnumerable<VsirDiagnostic> diagnostics) =>
        new(null, null, null, null, null, null, diagnostics.ToArray());
}
