namespace VSlices.Tooling.Tests;

public sealed class StructureDocumentCompatibilityTests
{
    [Fact]
    public async Task Structure_document_uses_the_same_progressive_authoring_mechanism_without_product_code_changes()
    {
        using var project = new ToolingTestProject();
        WriteDocsStandard(project.Root);
        var created = await project.Run(project.Root,
            "new", "document", "architecture", "--kind", "structure", "--target", "Tooling");
        Assert.Equal(0, created.ExitCode);
        var path = Path.Combine(project.Root, "architecture.md");
        var initial = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.Equal("# ¿Cómo se organiza?\n", ArtifactTestMetadata.Body(initial));
        var metadata = ArtifactTestMetadata.Read(initial);
        Assert.Equal("document", ArtifactTestMetadata.Scalar(metadata, "artifact", "kind"));
        Assert.Equal("structure", ArtifactTestMetadata.Scalar(metadata, "artifact", "type"));
        Assert.Equal("Tooling", ArtifactTestMetadata.Scalar(metadata, "artifact", "target"));
        Assert.DoesNotContain("¿Qué estructura estamos describiendo?", initial, StringComparison.Ordinal);
        var initialDiscovery = await project.Run(project.Root, "discovery", "document", "architecture");
        Assert.Equal(0, initialDiscovery.ExitCode);
        Assert.Contains("type: structure", initialDiscovery.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[1] ¿Cómo se organiza?", initialDiscovery.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: unanswered", initialDiscovery.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("¿Qué estructura estamos describiendo?", initialDiscovery.StandardOutput, StringComparison.Ordinal);
        var rootUpdate = await project.Run(project.Root, "update", "document", "architecture",
            "--question-id", "1", "--answer", "Se organiza como un conjunto de partes con responsabilidades explícitas.");
        Assert.Equal(0, rootUpdate.ExitCode);
        Assert.DoesNotContain("vslices:question question=structure", File.ReadAllText(path), StringComparison.Ordinal);
        var nextDiscovery = await project.Run(project.Root, "discovery", "document", "architecture");
        Assert.Equal(0, nextDiscovery.ExitCode);
        Assert.Contains("[1] ¿Cómo se organiza?", nextDiscovery.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: answered", nextDiscovery.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("[2] ¿Qué estructura estamos describiendo?", nextDiscovery.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("status: available", nextDiscovery.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("vslices update document architecture --question-id 2 --answer \"<answer>\"", nextDiscovery.StandardOutput, StringComparison.Ordinal);
        var childUpdate = await project.Run(project.Root, "update", "document", "architecture",
            "--question-id", "2", "--answer", "La estructura del sistema y sus partes principales.");
        Assert.Equal(0, childUpdate.ExitCode);
        var completed = File.ReadAllText(path);
        Assert.Contains("## ¿Qué estructura estamos describiendo?", completed, StringComparison.Ordinal);
        Assert.Contains("<!-- vslices:question question=structural-view -->", completed, StringComparison.Ordinal);
        Assert.DoesNotContain("vslices:question document=structure", completed, StringComparison.Ordinal);
        Assert.Contains("La estructura del sistema y sus partes principales.", completed, StringComparison.Ordinal);
    }

    private static void WriteDocsStandard(string projectRoot)
    {
        ToolingTestProject.WriteDocumentAuthoringSupport(projectRoot);
        var standardRoot = Path.Combine(projectRoot, ".vslices", "docs-standard");
        var documentsRoot = Path.Combine(standardRoot, "documents");
        Directory.CreateDirectory(documentsRoot);
        File.WriteAllText(Path.Combine(standardRoot, "manifest.yaml"), """
            kind: vslices-docs-standard
            version: 0.1
            documents:
              - documents/context-document.yml
              - documents/structure-document.yml
            """);
        File.WriteAllText(Path.Combine(documentsRoot, "context-document.yml"), """
            kind: vslices-document-definition
            version: 0.1

            document:
              type: context
              scopes:
                - concept
                - project
              question:
                id: context
                text: ¿Dónde existe?
                children:
                  - id: assumptions
                    text: ¿Qué estamos asumiendo como cierto?
            """);
        File.WriteAllText(Path.Combine(documentsRoot, "structure-document.yml"), """
            kind: vslices-document-definition
            version: 0.1

            document:
              type: structure
              scopes:
                - concept
                - process
                - flow
                - capability
                - product
                - service
                - project
                - solution
                - module
                - artifact-set
                - organization
              question:
                id: structure
                text: ¿Cómo se organiza?
                children:
                  - id: structural-view
                    text: ¿Qué estructura estamos describiendo?
            """);
    }
}
