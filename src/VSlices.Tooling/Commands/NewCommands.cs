using ConsoleAppFramework;

namespace VSlices.Tooling;

internal static class NewCommands
{
    /// <summary>Creates the minimal progressive VSIR artifact for a semantic name.</summary>
    /// <param name="name">Semantic name of the concept being introduced.</param>
    public static async Task<int> Vsir([Argument] string name, CancellationToken cancellationToken = default)
    {
        var result = VsirTemplate.Create(name);
        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(result.Error);
            return 2;
        }
        var defaultPath = Path.GetFullPath(name.EndsWith(".vsir", StringComparison.OrdinalIgnoreCase) ? name : name + ".vsir", Environment.CurrentDirectory);
        return await CommandInfrastructure.WriteResult(result.Source!, defaultPath, output: null, stdout: false, overwrite: false, cancellationToken);
    }

    /// <summary>Creates a progressive Document, optionally from a recommendation.</summary>
    /// <param name="name">Document name/path; derived from target and type when omitted.</param>
    /// <param name="kind">Document type from the installed Docs Standard.</param>
    /// <param name="target">Concrete target; required unless inherited from a recommendation.</param>
    /// <param name="scope">Explicit target classification.</param>
    /// <param name="fromNexus">Source Nexus and current selection: &lt;artifact&gt;:&lt;selection&gt;.</param>
    /// <param name="fromPath">Source Continuity Path and current selection: &lt;artifact&gt;:&lt;selection&gt;.</param>
    /// <param name="relatedTo">Arbitrary existing artifact to associate with; requires --role.</param>
    /// <param name="role">Concrete association role; overrides recommendation wording and is ignored when standalone.</param>
    public static Task<int> Document(
        [Argument] string? name = null, string? kind = null, string? target = null, string? scope = null,
        string? fromNexus = null, string? fromPath = null, string? relatedTo = null, string? role = null,
        CancellationToken cancellationToken = default) =>
        Create(new("document", name, kind, target, scope, fromNexus, fromPath, relatedTo, role), cancellationToken);

    /// <summary>Creates a Nexus composition artifact, optionally from a recommendation.</summary>
    public static Task<int> Nexus(
        [Argument] string? name = null, string? kind = null, string? target = null, string? scope = null,
        string? fromNexus = null, string? fromPath = null, string? relatedTo = null, string? role = null,
        CancellationToken cancellationToken = default) =>
        Create(new("nexus", name, kind, target, scope, fromNexus, fromPath, relatedTo, role), cancellationToken);

    /// <summary>Creates a Continuity Path trajectory artifact.</summary>
    public static Task<int> ContinuityPath(
        [Argument] string? name = null, string? kind = null, string? target = null, string? scope = null,
        string? relatedTo = null, string? role = null, CancellationToken cancellationToken = default) =>
        Create(new("continuity-path", name, kind, target, scope, null, null, relatedTo, role), cancellationToken);

    private static async Task<int> Create(KnowledgeArtifactCreationRequest request, CancellationToken cancellationToken)
    {
        var result = await KnowledgeArtifactCreation.Execute(request, Environment.CurrentDirectory, cancellationToken);
        if (result.Error is not null) TerminalOutput.Error(result.Error);
        else Console.WriteLine($"Created {request.Family} '{result.Path}'.");
        return result.ExitCode;
    }
}
