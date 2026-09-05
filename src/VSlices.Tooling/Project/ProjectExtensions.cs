using VSlices.Vsir;
using VSlices.Vsir.CSharp;
using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record ProjectExtensions(
    VsirValidationContext ValidationContext,
    IReadOnlyList<CSharpLoweringRule> CSharpRules)
{
    public static ProjectExtensions Empty { get; } =
        new(VsirValidationContext.Empty, []);
}

internal sealed record ProjectExtensionsLoadResult(
    ProjectExtensions? Extensions,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess => Extensions is not null && Diagnostics.Count == 0;
}

internal static class ProjectExtensionCatalogs
{
    private const string CurrentVersion = "0.1";

    public static ProjectExtensionsLoadResult Load(string extensionsRoot)
    {
        try
        {
            var root = Path.GetFullPath(extensionsRoot);
            if (!Directory.Exists(root))
                return Success(ProjectExtensions.Empty);

            var manifestPath = Path.Combine(root, "manifest.yaml");
            if (!File.Exists(manifestPath))
            {
                return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any()
                    ? Failure(
                        "EXT001",
                        $"Project extension directory '{root}' contains files but no manifest.yaml. Extension catalogs must be explicitly referenced from .vslices/extensions/manifest.yaml.")
                    : Success(ProjectExtensions.Empty);
            }

            var diagnostics = new List<VsirDiagnostic>();
            var manifest = LoadMapping(manifestPath);
            RejectUnknownKeys(
                manifest,
                ["version", "catalogs"],
                ".vslices/extensions/manifest.yaml",
                diagnostics);

            var version = RequiredScalar(
                manifest,
                "version",
                ".vslices/extensions/manifest.yaml",
                diagnostics);
            if (version is not null && !version.Equals(CurrentVersion, StringComparison.Ordinal))
            {
                diagnostics.Add(new(
                    "EXT002",
                    $"Unsupported project extension manifest version '{version}'. Only '{CurrentVersion}' is supported."));
            }

            if (!TryRequiredSequence(
                    manifest,
                    "catalogs",
                    ".vslices/extensions/manifest.yaml",
                    diagnostics,
                    out var catalogFiles))
            {
                return new(null, diagnostics);
            }

            var normalize = new HashSet<string>(StringComparer.Ordinal);
            var csharpRules = new List<CSharpLoweringRule>();
            var referencedCatalogs = new HashSet<string>(PathComparer());

            foreach (var catalogNode in catalogFiles.Children)
            {
                if (catalogNode is not YamlScalarNode catalogScalar ||
                    string.IsNullOrWhiteSpace(catalogScalar.Value))
                {
                    diagnostics.Add(new(
                        "EXT003",
                        "Project extension manifest 'catalogs' requires non-empty scalar path entries."));
                    continue;
                }

                var relativePath = catalogScalar.Value.Trim();
                var fullPath = Path.GetFullPath(relativePath, root);
                if (!IsWithin(root, fullPath))
                {
                    diagnostics.Add(new(
                        "EXT004",
                        $"Project extension catalog path '{relativePath}' escapes .vslices/extensions."));
                    continue;
                }

                if (!referencedCatalogs.Add(fullPath))
                {
                    diagnostics.Add(new(
                        "EXT005",
                        $"Project extension catalog '{relativePath}' is referenced more than once."));
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    diagnostics.Add(new(
                        "EXT006",
                        $"Project extension catalog '{relativePath}' does not exist."));
                    continue;
                }

                var catalog = LoadMapping(fullPath);
                RejectUnknownKeys(
                    catalog,
                    ["extensions"],
                    $"project extension catalog '{relativePath}'",
                    diagnostics);

                if (!TryRequiredSequence(
                        catalog,
                        "extensions",
                        $"project extension catalog '{relativePath}'",
                        diagnostics,
                        out var entries))
                {
                    continue;
                }

                foreach (var entryNode in entries.Children)
                {
                    if (entryNode is not YamlMappingNode entry)
                    {
                        diagnostics.Add(new(
                            "EXT007",
                            $"Project extension catalog '{relativePath}' requires mapping entries."));
                        continue;
                    }

                    RejectUnknownKeys(
                        entry,
                        ["node", "semantic", "targets"],
                        $"project extension entry in '{relativePath}'",
                        diagnostics);

                    var node = RequiredScalar(
                        entry,
                        "node",
                        $"project extension entry in '{relativePath}'",
                        diagnostics);
                    if (node is null)
                        continue;

                    if (!TryRequiredMapping(
                            entry,
                            "semantic",
                            $"project extension '{node}'",
                            diagnostics,
                            out var semantic))
                    {
                        continue;
                    }

                    RejectUnknownKeys(
                        semantic,
                        ["kind"],
                        $"project extension '{node}'.semantic",
                        diagnostics);

                    var kind = RequiredScalar(
                        semantic,
                        "kind",
                        $"project extension '{node}'.semantic",
                        diagnostics);
                    if (kind is null)
                        continue;

                    if (!kind.Equals("normalize", StringComparison.Ordinal))
                    {
                        diagnostics.Add(new(
                            "EXT008",
                            $"Project extension '{node}' uses unsupported semantic kind '{kind}'. Only 'normalize' is supported by this experiment."));
                        continue;
                    }

                    const string intrinsicPrefix = "intrinsic.";
                    if (!node.StartsWith(intrinsicPrefix, StringComparison.Ordinal) ||
                        node.Length == intrinsicPrefix.Length)
                    {
                        diagnostics.Add(new(
                            "EXT009",
                            $"Normalize project extension '{node}' must use an 'intrinsic.<name>' node identity."));
                        continue;
                    }

                    var intrinsic = node[intrinsicPrefix.Length..];
                    if (!normalize.Add(intrinsic))
                    {
                        diagnostics.Add(new(
                            "EXT010",
                            $"Normalize project extension '{intrinsic}' is declared more than once."));
                        continue;
                    }

                    if (!entry.Children.TryGetValue(new YamlScalarNode("targets"), out var targetsNode))
                        continue;

                    if (targetsNode is not YamlMappingNode targets)
                    {
                        diagnostics.Add(new(
                            "EXT011",
                            $"Project extension '{node}'.targets must be a mapping."));
                        continue;
                    }

                    RejectUnknownKeys(
                        targets,
                        ["csharp"],
                        $"project extension '{node}'.targets",
                        diagnostics);

                    if (!targets.Children.TryGetValue(new YamlScalarNode("csharp"), out var csharpNode))
                        continue;

                    if (csharpNode is not YamlMappingNode csharp)
                    {
                        diagnostics.Add(new(
                            "EXT012",
                            $"Project extension '{node}'.targets.csharp must be a mapping."));
                        continue;
                    }

                    RejectUnknownKeys(
                        csharp,
                        ["mode", "renderer", "template"],
                        $"project extension '{node}'.targets.csharp",
                        diagnostics);

                    var mode = RequiredScalar(
                        csharp,
                        "mode",
                        $"project extension '{node}'.targets.csharp",
                        diagnostics);
                    var renderer = RequiredScalar(
                        csharp,
                        "renderer",
                        $"project extension '{node}'.targets.csharp",
                        diagnostics);
                    var template = RequiredScalar(
                        csharp,
                        "template",
                        $"project extension '{node}'.targets.csharp",
                        diagnostics,
                        allowWhitespace: true);

                    if (mode is not null && renderer is not null && template is not null)
                        csharpRules.Add(new(node, mode, renderer, template));
                }
            }

            return diagnostics.Count == 0
                ? Success(new(
                    new VsirValidationContext(new VsirSemanticExtensions(normalize)),
                    csharpRules))
                : new(null, diagnostics);
        }
        catch (Exception ex)
        {
            return Failure("EXT000", ex.Message);
        }
    }

    private static ProjectExtensionsLoadResult Success(ProjectExtensions extensions) =>
        new(extensions, []);

    private static ProjectExtensionsLoadResult Failure(string code, string message) =>
        new(null, [new(code, message)]);

    private static YamlMappingNode LoadMapping(string path)
    {
        using var reader = File.OpenText(path);
        var yaml = new YamlStream();
        yaml.Load(reader);
        return yaml.Documents.Count == 1 && yaml.Documents[0].RootNode is YamlMappingNode root
            ? root
            : throw new InvalidDataException($"Expected one YAML mapping document in '{path}'.");
    }

    private static string? RequiredScalar(
        YamlMappingNode mapping,
        string key,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics,
        bool allowWhitespace = false)
    {
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out var node))
        {
            diagnostics.Add(new(
                "EXT013",
                $"{semanticPath} must declare '{key}'."));
            return null;
        }

        if (node is not YamlScalarNode scalar)
        {
            diagnostics.Add(new(
                "EXT014",
                $"{semanticPath}.{key} must be a scalar."));
            return null;
        }

        var value = scalar.Value ?? string.Empty;
        if (!allowWhitespace && string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(new(
                "EXT015",
                $"{semanticPath}.{key} must not be empty."));
            return null;
        }

        return value;
    }

    private static bool TryRequiredMapping(
        YamlMappingNode mapping,
        string key,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics,
        out YamlMappingNode result)
    {
        if (mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
            node is YamlMappingNode typed)
        {
            result = typed;
            return true;
        }

        diagnostics.Add(new(
            "EXT016",
            $"{semanticPath}.{key} must be a mapping."));
        result = null!;
        return false;
    }

    private static bool TryRequiredSequence(
        YamlMappingNode mapping,
        string key,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics,
        out YamlSequenceNode result)
    {
        if (mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) &&
            node is YamlSequenceNode typed)
        {
            result = typed;
            return true;
        }

        diagnostics.Add(new(
            "EXT017",
            $"{semanticPath}.{key} must be a sequence."));
        result = null!;
        return false;
    }

    private static void RejectUnknownKeys(
        YamlMappingNode mapping,
        IEnumerable<string> allowedKeys,
        string semanticPath,
        ICollection<VsirDiagnostic> diagnostics)
    {
        var allowed = allowedKeys.ToHashSet(StringComparer.Ordinal);
        foreach (var keyNode in mapping.Children.Keys)
        {
            if (keyNode is not YamlScalarNode scalar)
            {
                diagnostics.Add(new(
                    "EXT018",
                    $"{semanticPath} contains a non-scalar key."));
                continue;
            }

            var key = scalar.Value ?? string.Empty;
            if (!allowed.Contains(key))
            {
                diagnostics.Add(new(
                    "EXT019",
                    $"Unsupported project extension semantic '{semanticPath}.{key}'."));
            }
        }
    }

    private static bool IsWithin(string root, string path)
    {
        var normalizedRoot = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedRoot, PathComparison());
    }

    private static StringComparer PathComparer() =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private static StringComparison PathComparison() =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
