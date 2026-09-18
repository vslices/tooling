using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record DocumentFrontMatterReadResult(
    bool IsPresent,
    string? DocumentType,
    string? Error)
{
    public bool IsSuccess => Error is null;

    public static DocumentFrontMatterReadResult Missing() =>
        new(false, null, null);

    public static DocumentFrontMatterReadResult Success(string documentType) =>
        new(true, documentType, null);

    public static DocumentFrontMatterReadResult Failure(string error) =>
        new(true, null, error);
}

internal static class DocumentFrontMatter
{
    public static DocumentFrontMatterReadResult Read(string normalizedSource)
    {
        var lines = normalizedSource.Split('\n');
        if (lines.Length == 0 || !lines[0].Trim().Equals("---", StringComparison.Ordinal))
            return DocumentFrontMatterReadResult.Missing();

        var closingLine = -1;
        for (var index = 1; index < lines.Length; index++)
        {
            if (lines[index].Trim().Equals("---", StringComparison.Ordinal))
            {
                closingLine = index;
                break;
            }
        }

        if (closingLine < 0)
        {
            return DocumentFrontMatterReadResult.Failure(
                "DOCART017: Document front-matter is missing its closing '---' delimiter.");
        }

        var yamlText = string.Join("\n", lines.Skip(1).Take(closingLine - 1));
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(yamlText));

            if (yaml.Documents.Count != 1 ||
                yaml.Documents[0].RootNode is not YamlMappingNode root)
            {
                return DocumentFrontMatterReadResult.Failure(
                    "DOCART018: Document front-matter must contain exactly one YAML mapping.");
            }

            var unknownRootKey = FirstUnknownKey(root, "artifact");
            if (unknownRootKey is not null)
            {
                return DocumentFrontMatterReadResult.Failure(
                    $"DOCART019: Document front-matter contains unknown key '{unknownRootKey}'.");
            }

            if (!root.Children.TryGetValue(new YamlScalarNode("artifact"), out var artifactNode) ||
                artifactNode is not YamlMappingNode artifact)
            {
                return DocumentFrontMatterReadResult.Failure(
                    "DOCART020: Document front-matter must contain an artifact mapping.");
            }

            var unknownArtifactKey = FirstUnknownKey(artifact, "kind", "type");
            if (unknownArtifactKey is not null)
            {
                return DocumentFrontMatterReadResult.Failure(
                    $"DOCART021: Document artifact metadata contains unknown key '{unknownArtifactKey}'.");
            }

            if (!TryRequiredScalar(artifact, "kind", out var kind) ||
                !kind.Equals("document", StringComparison.Ordinal))
            {
                return DocumentFrontMatterReadResult.Failure(
                    "DOCART022: artifact.kind must be 'document'.");
            }

            if (!TryRequiredScalar(artifact, "type", out var type) ||
                !IsStableIdentifier(type))
            {
                return DocumentFrontMatterReadResult.Failure(
                    "DOCART023: artifact.type must be a stable identifier beginning with a letter and containing only letters, digits, '-' or '_'.");
            }

            return DocumentFrontMatterReadResult.Success(type);
        }
        catch (Exception ex)
        {
            return DocumentFrontMatterReadResult.Failure(
                $"DOCART024: Could not parse Document front-matter: {ex.Message}");
        }
    }

    private static string? FirstUnknownKey(
        YamlMappingNode mapping,
        params string[] allowed)
    {
        var set = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var pair in mapping.Children)
        {
            if (pair.Key is not YamlScalarNode scalar || string.IsNullOrWhiteSpace(scalar.Value))
                return "<non-scalar>";

            if (!set.Contains(scalar.Value))
                return scalar.Value;
        }

        return null;
    }

    private static bool TryRequiredScalar(
        YamlMappingNode mapping,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) ||
            node is not YamlScalarNode scalar ||
            string.IsNullOrWhiteSpace(scalar.Value))
        {
            return false;
        }

        value = scalar.Value.Trim();
        return true;
    }

    private static bool IsStableIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !char.IsLetter(value[0]))
            return false;

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsLetterOrDigit(character) && character is not '-' and not '_')
                return false;
        }

        return true;
    }
}
