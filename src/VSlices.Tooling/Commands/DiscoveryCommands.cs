using ConsoleAppFramework;
using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal static class DiscoveryCommands
{
    /// <summary>Shows the immediate semantic frontier plus always-available artifact metadata operations.</summary>
    /// <param name="artifact">VSIR symbol or path.</param>
    /// <param name="add">Projected collection additions as semicolon-separated path=value clauses. Add is available for searchable tags metadata and semantic traits.</param>
    /// <param name="remove">Projected removals as semicolon-separated clauses. Set-valued surfaces use path=value; removable assertions use path alone.</param>
    /// <param name="set">Projected assertions as semicolon-separated path=value clauses. Set establishes a missing assertion or replaces an existing one when the advertised path permits it.</param>
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

        // Conformance is evaluated in the semantic environment of the project
        // that owns the artifact. Discovery still does not prepare a target or
        // Ruleset because those facts belong to lowerability, not conformance.
        var project = VSlicesProjectContext.FindFrom(resolution.Path!);
        var validationContext = ProjectExtensions.Empty.ValidationContext;
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

        var source = await File.ReadAllTextAsync(resolution.Path!, cancellationToken);
        var projections = new List<VsirMutation>();
        var parseError = VsirMutationArgumentParser.AddMutations(projections, VsirMutationKind.Add, add, "DISC020")
            ?? VsirMutationArgumentParser.AddMutations(projections, VsirMutationKind.Remove, remove, "DISC020")
            ?? VsirMutationArgumentParser.AddMutations(projections, VsirMutationKind.Set, set, "DISC020");
        if (parseError is not null)
        {
            TerminalOutput.Error(parseError);
            return 2;
        }

        var inspectedSource = source;
        if (projections.Count > 0)
        {
            var projected = VsirMutationCandidate.Build(inspectedSource, projections);
            if (!projected.IsSuccess)
            {
                TerminalOutput.Error(projected.Error!.Replace("UPDATE", "DISC", StringComparison.Ordinal));
                return 2;
            }

            inspectedSource = projected.Source!;
        }

        var frontier = VsirMutationPipeline.Discover(inspectedSource, out var error).ToList();
        if (error is not null)
        {
            TerminalOutput.Error(error);
            return 2;
        }

        var state = VsirArtifactState.Assess(inspectedSource, frontier, validationContext);
        var semanticAuthoringGated =
            state.Conformance == VsirConformanceState.Conforming &&
            IsOutsidePublicSemanticAuthoringEnvelope(inspectedSource);

        // A canonical form can be executable/conforming before discovery/update
        // has proven public authoring parity for it. In that state we expose no
        // narrower semantic repair path; searchable metadata remains available.
        if (semanticAuthoringGated)
            frontier.Clear();

        frontier.Insert(0, VsirMetadataAuthoring.TagsContract);

        Console.WriteLine("Artifact state:");
        Console.WriteLine($"  progressive validity: {DisplayProgressiveValidity(state.ProgressiveValidity)}");
        Console.WriteLine($"  conformance: {DisplayConformance(state.Conformance)}");
        if (state.MissingRequiredPaths.Count > 0)
            Console.WriteLine($"  missing required: {string.Join(", ", state.MissingRequiredPaths)}");
        Console.WriteLine(semanticAuthoringGated
            ? "  public semantic authoring: gated for this conforming form"
            : "  public semantic authoring: represented by the immediate frontier");
        Console.WriteLine("  lowerability: not evaluated by discovery; it requires target, Ruleset and project context");

        if (projections.Count > 0)
            Console.WriteLine("Projected immediate frontier:");
        else
            Console.WriteLine("Immediate frontier:");

        foreach (var path in frontier)
        {
            Console.WriteLine();
            Console.WriteLine(path.Path);
            Console.WriteLine($"  status: {DisplayStatus(path.Status)}");
            Console.WriteLine($"  meaning: {path.Meaning}");
            Console.WriteLine($"  value kind: {VsirGrammarDiscovery.ValueKind(path)}");
            Console.WriteLine(path.Operations.Count == 0
                ? "  operations: not implemented"
                : $"  operations: {string.Join(", ", path.Operations.Select(DisplayOperation))}");
            if (path.AllowedValues is { Count: > 0 })
                Console.WriteLine($"  values: {string.Join(", ", path.AllowedValues)}");

            foreach (var template in VsirCommandTemplates.For(artifact, path))
                Console.WriteLine($"  command: {template}");

            var grammar = VsirGrammarDiscovery.For(path);
            if (grammar is not null)
            {
                Console.WriteLine($"  grammar root: {grammar.RootKind}");
                foreach (var form in grammar.Forms)
                {
                    Console.WriteLine($"  grammar {form.Name}: {form.Template}");
                    foreach (var slot in form.Slots)
                        Console.WriteLine($"    {slot.Name}: {slot.ValueKind}");
                }
            }
        }

        if (state.ConformanceDiagnostics.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Conformance diagnostics:");
            foreach (var diagnostic in state.ConformanceDiagnostics)
                Console.WriteLine($"  {diagnostic.Code}: {diagnostic.Message}");
        }

        return 0;
    }

    private static bool IsOutsidePublicSemanticAuthoringEnvelope(string source)
    {
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(source));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return false;

            var kind = Scalar(root, "kind");
            if (string.IsNullOrWhiteSpace(kind))
                return false;
            if (!VsirAuthoringContract.Kinds.Contains(kind, StringComparer.Ordinal))
                return true;
            if (!kind.Equals(VsirAuthoringContract.DomainTypeKind, StringComparison.Ordinal))
                return false;

            var shape = Scalar(root, "shape");
            if (!string.IsNullOrWhiteSpace(shape) &&
                !VsirAuthoringContract.DomainTypeShapes.Contains(shape, StringComparer.Ordinal))
            {
                return true;
            }

            var classification = Scalar(root, "classification");
            if (!string.IsNullOrWhiteSpace(classification) &&
                !VsirAuthoringContract.DomainTypeClassifications.Contains(classification, StringComparer.Ordinal))
            {
                return true;
            }

            if (root.Children.TryGetValue(new YamlScalarNode("traits"), out var traitsNode) &&
                traitsNode is YamlSequenceNode traits)
            {
                foreach (var traitNode in traits.Children.OfType<YamlScalarNode>())
                {
                    if (!string.IsNullOrWhiteSpace(traitNode.Value) &&
                        !VsirAuthoringContract.ExplicitDomainTypeTraits.Contains(traitNode.Value, StringComparer.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            // Syntax/conformance diagnostics are owned by the existing parser
            // paths. This helper only gates transitions after conformance has
            // already succeeded, so parse failure here cannot authorize more.
            return false;
        }
    }

    private static string? Scalar(YamlMappingNode root, string key) =>
        root.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode scalar
            ? scalar.Value
            : null;

    private static string DisplayOperation(VsirMutationKind kind) =>
        kind.ToString().ToLowerInvariant();

    private static string DisplayStatus(VsirFrontierStatus status) =>
        status.ToString().ToLowerInvariant();

    private static string DisplayProgressiveValidity(VsirProgressiveValidity validity) =>
        validity.ToString().ToLowerInvariant();

    private static string DisplayConformance(VsirConformanceState conformance) =>
        conformance.ToString().ToLowerInvariant();
}
