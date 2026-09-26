using static VSlices.Tooling.Tests.RelationalTestSupport;

namespace VSlices.Tooling.Tests;

public sealed class RelationalRecommendationEntryTests
{
    [Theory]
    [InlineData("nexus", "capability", "--from-nexus", "1", "document", "tooling-context")]
    [InlineData("nexus", "capability", "--from-nexus", "2", "nexus", "tooling-detail")]
    [InlineData("continuity-path", "domain-context", "--from-path", "3.1.1", "document", "tooling-context")]
    [InlineData("continuity-path", "domain-context", "--from-path", "3.1.2.1", "nexus", "tooling-capability")]
    public async Task Prepared_recommendation_commands_work_without_positional_name(
        string sourceFamily, string sourceType, string flag, string selection, string family, string expectedName)
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        await Success(project, "new", sourceFamily, "origin", "--kind", sourceType, "--target", "Tooling");
        var discovery = await Success(project, "discovery", sourceFamily, "origin");
        Assert.Contains($"vslices new {family} {flag} origin:{selection}", discovery, StringComparison.Ordinal);
        await Success(project, "new", family, flag, "origin:" + selection);
        Assert.True(File.Exists(Path.Combine(project.Root, expectedName + ".md")));
        Assert.Equal("Tooling", Value(Metadata(project, expectedName), "artifact", "target"));
        Assert.Equal("origin.md", Value(Assert.Single(Relations(project, expectedName)), "target"));
        Assert.Contains("status: created", Selection(await Success(project, "discovery", sourceFamily, "origin"), selection), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pinned_real_candidates_support_unanswered_path_navigation_and_nexus_materialization()
    {
        using var project = new ToolingTestProject();
        WriteEnvironment(project);
        File.WriteAllText(Path.Combine(StandardRoot(project), "nexus", "capability.yml"), RealCapability);
        File.WriteAllText(Path.Combine(StandardRoot(project), "continuity-paths", "domain-context.yml"), RealDomainPath);
        await Success(project, "new", "continuity-path", "domain", "--kind", "domain-context", "--target", "Domain");
        var discovery = await Success(project, "discovery", "continuity-path", "domain");
        Assert.Contains("¿Qué capacidades nacen desde este dominio?", discovery, StringComparison.Ordinal);
        Assert.Contains("vslices new nexus --from-path domain:3.4.1", discovery, StringComparison.Ordinal);
        Assert.Contains("vslices new document --from-path domain:3.5.1", discovery, StringComparison.Ordinal);
        await Success(project, "new", "nexus", "--from-path", "domain:3.4.1");
        var nexus = await Success(project, "discovery", "nexus", "domain-capability");
        Assert.Equal("capability", Value(Metadata(project, "domain-capability"), "artifact", "scope"));
        Assert.Contains("[1] document: scope", nexus, StringComparison.Ordinal);
        Assert.Contains("[6] document: decision-record", nexus, StringComparison.Ordinal);
        Assert.Contains("domain.md", nexus, StringComparison.Ordinal);
        // The Document definition is the synthetic context fixture; candidate Path and Nexus definitions are verbatim snapshots.
        await Success(project, "new", "document", "--from-path", "domain:3.5.1");
        Assert.Equal("Domain", Value(Metadata(project, "domain-context"), "artifact", "target"));
        Assert.Equal(2, Relations(project, "domain").Length);
    }

    // Verbatim candidates from vslices/docs-standard@afdd900d7d44ceba08e7d05d3ad0d78c50841cae.
    // nexus/capability-nexus.yml blob c6a7177e689835fa3f08b5ed443948df813e677b.
    private const string RealCapability = """
        kind: vslices-nexus-definition
        version: 0.1

        nexus:
          type: capability

          scopes:
            - capability

          recommendations:
            - document: scope
              role: Define los límites de la capacidad

            - document: behavior
              role: Explica qué debe ocurrir al ejercer la capacidad

            - document: structure
              role: Explica cómo se organiza la capacidad

            - document: consistency
              role: Explica qué debe mantenerse coherente dentro de la capacidad

            - document: viability
              role: Evalúa si la capacidad puede sostenerse

            - document: decision-record
              role: Preserva las decisiones que delimitan o justifican la capacidad
        """;

    // continuity-paths/domain-context.yml blob 9f4f604a4f1a03c20e749fe748eb43521d627eaf.
    private const string RealDomainPath = """
        kind: vslices-continuity-path-definition
        version: 0.1

        continuity-path:
          type: domain-context

          purpose: >
            Preserva continuidad entre un contexto de dominio, su lenguaje, conceptos,
            comportamientos y las capacidades o realizaciones que dependen de ese significado.

          recommended-traversal: >
            Comenzar por el contexto, reconocer su lenguaje y conceptos, seguir sus
            comportamientos y capacidades, y terminar observando qué productos,
            servicios y decisiones dependen de esos significados.

          question:
            id: domain-context
            text: ¿Qué lenguaje, conceptos, reglas y comportamientos pertenecen a este contexto?

            children:
              - id: domain-language
                text: ¿Qué lenguaje pertenece a este contexto?
                connection:
                  text: se expresa mediante
                recommendations:
                  - document: domain-vocabulary
                    role: Preserva los términos y significados propios del contexto

              - id: domain-concepts
                text: ¿Qué conceptos abarca?
                connection:
                  text: contiene
                recommendations:
                  - document: domain-vocabulary
                    role: Preserva los conceptos incluidos en el contexto
                  - document: consistency
                    role: Preserva reglas o invariantes que deben mantenerse coherentes

              - id: domain-behaviors
                text: ¿Qué comportamientos expresa?
                connection:
                  text: se manifiesta mediante
                recommendations:
                  - document: behavior
                    role: Preserva los comportamientos propios del contexto

              - id: domain-capabilities
                text: ¿Qué capacidades nacen desde este dominio?
                connection:
                  text: habilita
                recommendations:
                  - nexus: capability
                    role: Compone las perspectivas necesarias para comprender una capacidad nacida del dominio

              - id: domain-consumers
                text: ¿Qué productos o servicios usan este significado?
                connection:
                  text: es utilizado por
                recommendations:
                  - document: context
                    role: Preserva dónde se utiliza el significado del dominio

              - id: context-boundaries
                text: ¿Qué decisiones delimitan este contexto?
                connection:
                  text: está delimitado por
                recommendations:
                  - document: decision-record
                    role: Preserva las decisiones que explican los límites del contexto
        """;
}
