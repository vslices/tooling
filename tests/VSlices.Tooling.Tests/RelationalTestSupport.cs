using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling.Tests;

internal static class RelationalTestSupport
{
    public static void WriteEnvironment(ToolingTestProject project)
    {
        ToolingTestProject.WriteDocumentAuthoringSupport(project.Root);
        var root = StandardRoot(project);
        Directory.CreateDirectory(Path.Combine(root, "documents"));
        Directory.CreateDirectory(Path.Combine(root, "nexus"));
        Directory.CreateDirectory(Path.Combine(root, "continuity-paths"));
        File.WriteAllText(Path.Combine(root, "manifest.yaml"), """
            kind: vslices-docs-standard
            version: 0.1
            documents:
              - documents/context.yml
            """);
        File.WriteAllText(Path.Combine(root, "documents", "context.yml"), """
            kind: vslices-document-definition
            version: 0.1
            document:
              type: context
              scopes: [project, concept]
              question:
                id: context
                text: Where does it exist?
                children:
                  - id: assumptions
                    text: What do we assume?
            """);
        File.WriteAllText(Path.Combine(root, "nexus", "capability.yml"), Capability);
        File.WriteAllText(Path.Combine(root, "nexus", "detail.yml"), Detail);
        File.WriteAllText(Path.Combine(root, "continuity-paths", "domain-context.yml"), DomainPath);
    }

    public const string Capability = """
        kind: vslices-nexus-definition
        version: 0.1
        nexus:
          type: capability
          scopes: [project]
          recommendations:
            - document: context
              role: Explains the context
            - nexus: detail
              role: Develops a perspective
        """;

    public const string Detail = """
        kind: vslices-nexus-definition
        version: 0.1
        nexus:
          type: detail
          scopes: [project, concept]
          questions:
            - id: perspective
              text: Which perspective?
              default: Existing perspective
              children:
                - id: boundaries
                  text: What boundaries?
                  recommendations:
                    - document: context
                      role: Explains the boundary
        """;

    public const string DomainPath = """
        kind: vslices-continuity-path-definition
        version: 0.1
        continuity-path:
          type: domain-context
          purpose: Preserve contextual continuity
          recommended-traversal: Begin with the context and follow its perspectives
          question:
            id: root
            text: Which context?
            children:
              - id: a-b
                text: 'What "knowledge" matters?'
                connection:
                  text: explains
                recommendations:
                  - document: context
                    role: Preserves knowledge
                children:
                  - id: a_b
                    text: Which capability composes it?
                    connection:
                      text: composes
                    recommendations:
                      - nexus: capability
                        role: Composes the capability
        """;

    public static string StandardRoot(ToolingTestProject project) => Path.Combine(project.Root, ".vslices", "docs-standard");
    public static string Read(ToolingTestProject project, string name) => File.ReadAllText(Path.Combine(project.Root, name + ".md")).Replace("\r\n", "\n", StringComparison.Ordinal);
    public static YamlMappingNode Metadata(ToolingTestProject project, string name) => ArtifactTestMetadata.Read(Read(project, name));
    public static YamlMappingNode[] Relations(ToolingTestProject project, string name) =>
        Assert.IsType<YamlSequenceNode>(ArtifactTestMetadata.Node(Metadata(project, name), "metadata", "relates"))
            .Children.Select(node => Assert.IsType<YamlMappingNode>(node)).ToArray();
    public static string Value(YamlMappingNode mapping, params string[] path) => ArtifactTestMetadata.Scalar(mapping, path);

    public static async Task<string> Success(ToolingTestProject project, params string[] arguments)
    {
        var result = await project.Run(project.Root, arguments);
        Assert.True(result.ExitCode == 0, $"{string.Join(' ', arguments)}\n{result.StandardError}\n{result.StandardOutput}");
        return result.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public static async Task<string> Failure(ToolingTestProject project, params string[] arguments)
    {
        var result = await project.Run(project.Root, arguments);
        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("Created ", result.StandardOutput, StringComparison.Ordinal);
        return result.StandardError;
    }

    public static string Selection(string output, string id)
    {
        var start = output.IndexOf("\n[" + id + "] ", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing selection [{id}] in:\n{output}");
        var next = output.IndexOf("\n[", start + 1, StringComparison.Ordinal);
        var associations = output.IndexOf("\nArtifacts asociados:", start + 1, StringComparison.Ordinal);
        var end = next < 0 ? output.Length : next;
        if (associations >= 0) end = Math.Min(end, associations);
        return output[start..end];
    }

    public static string Graph(string source)
    {
        var start = source.IndexOf("```mermaid\n", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = source.IndexOf("\n```", start + "```mermaid\n".Length, StringComparison.Ordinal);
        Assert.True(end > start);
        return source[start..end];
    }
}
