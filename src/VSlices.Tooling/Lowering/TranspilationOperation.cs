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

        var prepared = Prepare(resolution.Path!, requestedTarget);
        if (!prepared.IsSuccess)
            return TranspilationResult.Failure(prepared.Diagnostics);

        return await ExecuteResolved(
            resolution.Path!,
            prepared.Environment!,
            namespaceOverride,
            cancellationToken);
    }

    public static TranspilationEnvironmentResult Prepare(
        string projectEvidencePath,
        string? requestedTarget)
    {
        var project = VSlicesProjectContext.FindFrom(projectEvidencePath);
        if (project is null || !File.Exists(Path.Combine(project.RulesetRoot, "manifest.yaml")))
        {
            return TranspilationEnvironmentResult.Failure([new(
                "CLI010",
                "No project-local VSlices project/ruleset was found. Expected .vslices/config.yaml and .vslices/ruleset/manifest.yaml in the path ancestry. Run 'vslices init'.")]);
        }

        var target = CommandInfrastructure.ResolveTarget(requestedTarget, project);
        if (target.Diagnostic is not null)
            return TranspilationEnvironmentResult.Failure([target.Diagnostic]);

        if (target.Target != "csharp")
        {
            return TranspilationEnvironmentResult.Failure([new(
                "CLI020",
                $"Target '{target.Target}' is not supported by the current lowering engine.")]);
        }

        var extensions = ProjectExtensionCatalogs.Load(project.ExtensionsRoot);
        if (!extensions.IsSuccess)
            return TranspilationEnvironmentResult.Failure(extensions.Diagnostics);

        var rules = CSharpLoweringRuleSet.Load(
            project.RulesetRoot,
            extensions.Extensions!.CSharpRules);
        if (!rules.IsSuccess)
            return TranspilationEnvironmentResult.Failure(rules.Diagnostics);

        return TranspilationEnvironmentResult.Success(new(
            project,
            target.Target!,
            extensions.Extensions,
            rules.RuleSet!));
    }

    public static async Task<TranspilationResult> ExecuteResolved(
        string vsirPath,
        TranspilationEnvironment environment,
        string? namespaceOverride,
        CancellationToken cancellationToken)
    {
        var targetContext = await DotNetTargetContextResolver.Resolve(
            vsirPath,
            namespaceOverride,
            cancellationToken,
            environment.Project.Configuration.CSharpNamespaceIgnoredFolders);

        if (targetContext.Diagnostic is not null)
            return TranspilationResult.Failure([targetContext.Diagnostic]);

        var text = await File.ReadAllTextAsync(vsirPath, cancellationToken);
        var parsed = VsirParser.Parse(text, environment.Extensions.ValidationContext);
        if (!parsed.IsSuccess)
            return TranspilationResult.Failure(parsed.Diagnostics);

        var lowered = CSharpLowerer.Lower(
            parsed.Document!,
            new CSharpLoweringContext(
                targetContext.Context!.Namespace,
                environment.RuleSet,
                environment.Extensions.ValidationContext));

        return lowered.IsSuccess
            ? TranspilationResult.Success(
                lowered.Source!,
                vsirPath,
                parsed.Document!.Name,
                environment.Target,
                environment.Project,
                targetContext.Context!)
            : TranspilationResult.Failure(lowered.Diagnostics);
    }
}

internal sealed record TranspilationEnvironment(
    VSlicesProjectContext Project,
    string Target,
    ProjectExtensions Extensions,
    CSharpLoweringRuleSet RuleSet);

internal sealed record TranspilationEnvironmentResult(
    TranspilationEnvironment? Environment,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess => Environment is not null && Diagnostics.Count == 0;

    public static TranspilationEnvironmentResult Success(TranspilationEnvironment environment) =>
        new(environment, []);

    public static TranspilationEnvironmentResult Failure(IEnumerable<VsirDiagnostic> diagnostics) =>
        new(null, diagnostics.ToArray());
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
