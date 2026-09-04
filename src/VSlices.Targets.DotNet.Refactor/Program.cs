using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

var options = RefactorArguments.Parse(args);
if (options is null)
{
    Console.Error.WriteLine("Usage: VSlices.Targets.DotNet.Refactor --project <csproj> --document <cs> --candidate <cs> --symbol <name> --staging <dir> --manifest <json>");
    return 2;
}

try
{
    if (!MSBuildLocator.IsRegistered)
        MSBuildLocator.RegisterDefaults();

    return await NamespaceMovePlanner.Execute(options, CancellationToken.None);
}
catch (Exception ex)
{
    await RefactorManifest.WriteFailure(
        options.ManifestPath,
        "DOTNET020",
        $"Roslyn semantic refactoring failed: {ex.Message}");
    return 1;
}

internal sealed record RefactorArguments(
    string ProjectPath,
    string DocumentPath,
    string CandidatePath,
    string SymbolName,
    string StagingPath,
    string ManifestPath)
{
    public static RefactorArguments? Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                return null;
            values[args[index][2..]] = args[++index];
        }

        return Try("project", out var project) &&
               Try("document", out var document) &&
               Try("candidate", out var candidate) &&
               Try("symbol", out var symbol) &&
               Try("staging", out var staging) &&
               Try("manifest", out var manifest)
            ? new(
                Path.GetFullPath(project!),
                Path.GetFullPath(document!),
                Path.GetFullPath(candidate!),
                symbol!,
                Path.GetFullPath(staging!),
                Path.GetFullPath(manifest!))
            : null;

        bool Try(string key, out string? value) =>
            values.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value);
    }
}

internal static class NamespaceMovePlanner
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<int> Execute(
        RefactorArguments options,
        CancellationToken cancellationToken)
    {
        var solutionPath = FindUniqueSolution(options.ProjectPath);
        if (solutionPath is null)
        {
            await RefactorManifest.WriteFailure(
                options.ManifestPath,
                "DOTNET021",
                "A unique .sln or .slnx could not be established for semantic blast-radius analysis. No files were modified.");
            return 1;
        }

        var workspaceFailures = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
#pragma warning disable CS0618
        workspace.WorkspaceFailed += (_, eventArgs) =>
        {
            if (eventArgs.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                workspaceFailures.Add(eventArgs.Diagnostic.Message);
        };
#pragma warning restore CS0618

        var solution = await workspace.OpenSolutionAsync(
            solutionPath,
            cancellationToken: cancellationToken);

        if (workspaceFailures.Count > 0)
        {
            await RefactorManifest.WriteFailure(
                options.ManifestPath,
                "DOTNET022",
                "Roslyn could not load the solution completely: " + string.Join(" | ", workspaceFailures.Take(5)));
            return 1;
        }

        var project = solution.Projects.SingleOrDefault(x => PathEquals(x.FilePath, options.ProjectPath));
        if (project is null)
        {
            await RefactorManifest.WriteFailure(
                options.ManifestPath,
                "DOTNET023",
                $"Project '{options.ProjectPath}' was not found in '{solutionPath}'.");
            return 1;
        }

        var document = project.Documents.SingleOrDefault(x => PathEquals(x.FilePath, options.DocumentPath));
        if (document is null)
        {
            await RefactorManifest.WriteFailure(
                options.ManifestPath,
                "DOTNET024",
                $"Materialization '{options.DocumentPath}' is not a source document in '{options.ProjectPath}'.");
            return 1;
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || semanticModel is null)
        {
            await RefactorManifest.WriteFailure(
                options.ManifestPath,
                "DOTNET025",
                "Roslyn could not build a syntax/semantic model for the materialization.");
            return 1;
        }

        var declarations = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(x => x.Identifier.ValueText.Equals(options.SymbolName, StringComparison.Ordinal))
            .ToArray();
        if (declarations.Length != 1)
        {
            await RefactorManifest.WriteFailure(
                options.ManifestPath,
                "DOTNET026",
                $"Expected exactly one declaration of '{options.SymbolName}' in the materialization, found {declarations.Length}.");
            return 1;
        }

        var symbol = semanticModel.GetDeclaredSymbol(declarations[0], cancellationToken) as INamedTypeSymbol;
        if (symbol is null || symbol.Arity != 0 || symbol.ContainingType is not null)
        {
            await RefactorManifest.WriteFailure(
                options.ManifestPath,
                "DOTNET027",
                "The current namespace-move refactoring requires one non-generic top-level named type.");
            return 1;
        }

        var candidateText = await File.ReadAllTextAsync(options.CandidatePath, cancellationToken);
        var candidateTree = CSharpSyntaxTree.ParseText(
            candidateText,
            document.Project.ParseOptions as CSharpParseOptions,
            cancellationToken: cancellationToken);
        var candidateRoot = await candidateTree.GetRootAsync(cancellationToken);
        var candidateDeclarations = candidateRoot.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(x => x.Identifier.ValueText.Equals(options.SymbolName, StringComparison.Ordinal))
            .ToArray();
        if (candidateDeclarations.Length != 1)
        {
            await RefactorManifest.WriteFailure(
                options.ManifestPath,
                "DOTNET028",
                $"Candidate materialization must contain exactly one declaration of '{options.SymbolName}'.");
            return 1;
        }

        var oldNamespace = symbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : symbol.ContainingNamespace.ToDisplayString();
        var newNamespace = NamespaceOf(candidateDeclarations[0]);
        if (string.Equals(oldNamespace, newNamespace, StringComparison.Ordinal))
        {
            await RefactorManifest.Write(
                options.ManifestPath,
                RefactorManifest.SuccessNoMove(symbol.Name, oldNamespace));
            return 0;
        }

        var references = await SymbolFinder.FindReferencesAsync(
            symbol,
            solution,
            cancellationToken);
        var locations = references
            .SelectMany(x => x.Locations)
            .Where(x => x.Location.IsInSource && !x.IsImplicit)
            .ToArray();

        var referenceSites = new List<ReferenceSite>();
        foreach (var location in locations)
        {
            var referenceDocument = location.Document;
            if (referenceDocument.Project.Language != LanguageNames.CSharp || string.IsNullOrWhiteSpace(referenceDocument.FilePath))
            {
                await RefactorManifest.WriteFailure(
                    options.ManifestPath,
                    "DOTNET029",
                    "The namespace move has a reference that is not a writable C# source document. Automatic refactoring was not attempted.");
                return 1;
            }

            var referenceRoot = await referenceDocument.GetSyntaxRootAsync(cancellationToken);
            var referenceModel = await referenceDocument.GetSemanticModelAsync(cancellationToken);
            if (referenceRoot is null || referenceModel is null)
            {
                await RefactorManifest.WriteFailure(
                    options.ManifestPath,
                    "DOTNET030",
                    $"Could not inspect semantic reference in '{referenceDocument.FilePath}'.");
                return 1;
            }

            var node = referenceRoot.FindNode(
                location.Location.SourceSpan,
                getInnermostNodeForTie: true,
                findInsideTrivia: true);
            var name = node.AncestorsAndSelf()
                .OfType<NameSyntax>()
                .Where(x => ResolvesTo(referenceModel, x, symbol, cancellationToken))
                .OrderByDescending(x => x.Span.Length)
                .FirstOrDefault();
            if (name is null)
            {
                await RefactorManifest.WriteFailure(
                    options.ManifestPath,
                    "DOTNET031",
                    $"Reference at '{referenceDocument.FilePath}:{location.Location.GetLineSpan().StartLinePosition.Line + 1}' could not be rewritten conservatively.");
                return 1;
            }

            referenceSites.Add(new(referenceDocument.Id, name.Span));
        }

        var oldText = await document.GetTextAsync(cancellationToken);
        var candidateSourceText = SourceText.From(
            candidateText,
            oldText.Encoding ?? new UTF8Encoding(false));
        var candidateSolution = solution.WithDocumentText(
            document.Id,
            candidateSourceText,
            PreservationMode.PreserveIdentity);
        var candidateDocument = candidateSolution.GetDocument(document.Id)!;
        var candidateSemanticModel = await candidateDocument.GetSemanticModelAsync(cancellationToken);
        var loadedCandidateRoot = await candidateDocument.GetSyntaxRootAsync(cancellationToken);
        if (candidateSemanticModel is null || loadedCandidateRoot is null)
        {
            await RefactorManifest.WriteFailure(
                options.ManifestPath,
                "DOTNET032",
                "Roslyn could not bind the candidate materialization after the namespace move.");
            return 1;
        }

        var loadedCandidateDeclarations = loadedCandidateRoot.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(x => x.Identifier.ValueText.Equals(options.SymbolName, StringComparison.Ordinal))
            .ToArray();
        var movedSymbol = loadedCandidateDeclarations.Length == 1
            ? candidateSemanticModel.GetDeclaredSymbol(loadedCandidateDeclarations[0], cancellationToken) as INamedTypeSymbol
            : null;
        if (movedSymbol is null)
        {
            await RefactorManifest.WriteFailure(
                options.ManifestPath,
                "DOTNET032",
                "Roslyn could not resolve the moved symbol in the candidate materialization.");
            return 1;
        }

        var requiredSpansByDocument = new Dictionary<DocumentId, HashSet<TextSpan>>();
        foreach (var site in referenceSites)
        {
            var mappedSpan = site.Span;
            if (site.DocumentId == document.Id &&
                !TryMapSpan(oldText.ToString(), candidateText, site.Span, out mappedSpan))
            {
                await RefactorManifest.WriteFailure(
                    options.ManifestPath,
                    "DOTNET032",
                    "A semantic reference overlaps the deterministic rebase delta in the materialization. No automatic refactoring was attempted.");
                return 1;
            }

            var reboundDocument = candidateSolution.GetDocument(site.DocumentId)!;
            var reboundRoot = await reboundDocument.GetSyntaxRootAsync(cancellationToken);
            var reboundModel = await reboundDocument.GetSemanticModelAsync(cancellationToken);
            if (reboundRoot is null || reboundModel is null)
            {
                await RefactorManifest.WriteFailure(
                    options.ManifestPath,
                    "DOTNET030",
                    $"Could not rebind semantic reference in '{reboundDocument.FilePath}'.");
                return 1;
            }

            var reboundNode = reboundRoot.FindNode(
                mappedSpan,
                getInnermostNodeForTie: true,
                findInsideTrivia: true);
            var stillResolves = reboundNode.AncestorsAndSelf()
                .OfType<NameSyntax>()
                .Any(x => ResolvesTo(reboundModel, x, movedSymbol, cancellationToken));
            if (stillResolves)
                continue;

            if (!requiredSpansByDocument.TryGetValue(site.DocumentId, out var spans))
            {
                spans = [];
                requiredSpansByDocument.Add(site.DocumentId, spans);
            }
            spans.Add(mappedSpan);
        }

        var replacement = FullyQualified(newNamespace, symbol.Name);
        var proposedTexts = new Dictionary<DocumentId, SourceText>();
        var referenceCounts = new Dictionary<DocumentId, int>();

        foreach (var pair in requiredSpansByDocument)
        {
            var referenceDocument = candidateSolution.GetDocument(pair.Key)!;
            var sourceText = await referenceDocument.GetTextAsync(cancellationToken);
            var changes = pair.Value
                .OrderBy(x => x.Start)
                .Select(span => new TextChange(span, replacement))
                .ToArray();
            proposedTexts[pair.Key] = sourceText.WithChanges(changes);
            referenceCounts[pair.Key] = changes.Length;
        }

        if (!proposedTexts.ContainsKey(document.Id))
        {
            proposedTexts[document.Id] = candidateSourceText;
            referenceCounts[document.Id] = 0;
        }

        var changedProjectIds = proposedTexts.Keys
            .Select(id => solution.GetDocument(id)!.Project.Id)
            .Distinct()
            .ToArray();

        foreach (var projectId in changedProjectIds)
        {
            var before = await solution.GetProject(projectId)!.GetCompilationAsync(cancellationToken);
            if (before is null)
                continue;
            var errors = before.GetDiagnostics(cancellationToken)
                .Where(x => x.Severity == DiagnosticSeverity.Error)
                .Take(5)
                .ToArray();
            if (errors.Length > 0)
            {
                await RefactorManifest.WriteFailure(
                    options.ManifestPath,
                    "DOTNET033",
                    "Semantic refactoring cannot be validated transactionally because an affected project already has compiler errors: " +
                    string.Join(" | ", errors.Select(FormatDiagnostic)));
                return 1;
            }
        }

        var updatedSolution = candidateSolution;
        foreach (var pair in proposedTexts)
            updatedSolution = updatedSolution.WithDocumentText(pair.Key, pair.Value, PreservationMode.PreserveIdentity);

        foreach (var projectId in changedProjectIds)
        {
            var after = await updatedSolution.GetProject(projectId)!.GetCompilationAsync(cancellationToken);
            if (after is null)
                continue;
            var errors = after.GetDiagnostics(cancellationToken)
                .Where(x => x.Severity == DiagnosticSeverity.Error)
                .Take(10)
                .ToArray();
            if (errors.Length > 0)
            {
                await RefactorManifest.WriteFailure(
                    options.ManifestPath,
                    "DOTNET034",
                    "The proposed namespace refactoring does not compile. No files were modified: " +
                    string.Join(" | ", errors.Select(FormatDiagnostic)));
                return 1;
            }
        }

        Directory.CreateDirectory(options.StagingPath);
        var files = new List<RefactorFile>();
        var index = 0;
        foreach (var pair in proposedTexts.OrderBy(x => solution.GetDocument(x.Key)!.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            var changedDocument = solution.GetDocument(pair.Key)!;
            var path = Path.GetFullPath(changedDocument.FilePath!);
            var original = await changedDocument.GetTextAsync(cancellationToken);
            if (original.ContentEquals(pair.Value))
                continue;

            var staged = Path.Combine(
                options.StagingPath,
                $"{index++:D4}-{Path.GetFileName(path)}");
            var encoding = original.Encoding ?? new UTF8Encoding(false);
            await File.WriteAllTextAsync(staged, pair.Value.ToString(), encoding, cancellationToken);
            files.Add(new(
                path,
                staged,
                Sha256(path),
                referenceCounts.GetValueOrDefault(pair.Key)));
        }

        var requiredReferenceCount = referenceCounts.Values.Sum();
        var oldDisplay = FullyQualified(oldNamespace, symbol.Name).Replace("global::", string.Empty, StringComparison.Ordinal);
        var newDisplay = replacement.Replace("global::", string.Empty, StringComparison.Ordinal);
        await RefactorManifest.Write(
            options.ManifestPath,
            new(
                true,
                true,
                requiredReferenceCount > 0,
                oldDisplay,
                newDisplay,
                requiredReferenceCount,
                files,
                []));
        return 0;
    }

    private static bool ResolvesTo(
        SemanticModel model,
        NameSyntax name,
        INamedTypeSymbol target,
        CancellationToken cancellationToken)
    {
        var info = model.GetSymbolInfo(name, cancellationToken);
        var symbol = info.Symbol is IAliasSymbol alias ? alias.Target : info.Symbol;
        if (SymbolEqualityComparer.Default.Equals(symbol, target))
            return true;

        var type = model.GetTypeInfo(name, cancellationToken).Type;
        return SymbolEqualityComparer.Default.Equals(type, target);
    }

    private static string NamespaceOf(TypeDeclarationSyntax declaration)
    {
        var parts = declaration.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Reverse()
            .Select(x => x.Name.ToString())
            .ToArray();
        return string.Join('.', parts);
    }

    private static string FullyQualified(string namespaceName, string symbolName) =>
        namespaceName.Length == 0
            ? $"global::{symbolName}"
            : $"global::{namespaceName}.{symbolName}";

    private static bool TryMapSpan(
        string previous,
        string next,
        TextSpan previousSpan,
        out TextSpan nextSpan)
    {
        var prefix = CommonPrefixLength(previous, next);
        var suffix = CommonSuffixLength(previous, next, prefix);
        var previousChangedEnd = previous.Length - suffix;
        var nextChangedEnd = next.Length - suffix;

        if (previousSpan.End <= prefix)
        {
            nextSpan = previousSpan;
            return true;
        }

        if (previousSpan.Start >= previousChangedEnd)
        {
            var delta = nextChangedEnd - previousChangedEnd;
            nextSpan = new(previousSpan.Start + delta, previousSpan.Length);
            return true;
        }

        nextSpan = default;
        return false;
    }

    private static int CommonPrefixLength(string left, string right)
    {
        var max = Math.Min(left.Length, right.Length);
        var index = 0;
        while (index < max && left[index] == right[index])
            index++;
        return index;
    }

    private static int CommonSuffixLength(string left, string right, int prefixLength)
    {
        var max = Math.Min(left.Length - prefixLength, right.Length - prefixLength);
        var count = 0;
        while (count < max && left[left.Length - 1 - count] == right[right.Length - 1 - count])
            count++;
        return count;
    }

    private static string? FindUniqueSolution(string projectPath)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        for (var current = new DirectoryInfo(projectDirectory); current is not null; current = current.Parent)
        {
            var solutions = current.GetFiles("*.slnx", SearchOption.TopDirectoryOnly)
                .Concat(current.GetFiles("*.sln", SearchOption.TopDirectoryOnly))
                .ToArray();
            if (solutions.Length == 1)
                return solutions[0].FullName;
            if (solutions.Length > 1)
                return null;
        }
        return null;
    }

    private static bool PathEquals(string? left, string right) =>
        left is not null &&
        Path.GetFullPath(left).Equals(
            Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location.IsInSource
            ? $"{diagnostic.Location.GetLineSpan().Path}:{diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}"
            : "<project>";
        return $"{diagnostic.Id} {location} {diagnostic.GetMessage()}";
    }

    private sealed record ReferenceSite(DocumentId DocumentId, TextSpan Span);
}

internal sealed record RefactorFile(
    string Path,
    string StagedPath,
    string OriginalSha256,
    int ReferenceCount);

internal sealed record RefactorDiagnostic(string Code, string Message);

internal sealed record RefactorManifest(
    bool Success,
    bool NamespaceChanged,
    bool RequiresAuthorization,
    string? PreviousSymbol,
    string? NextSymbol,
    int ReferenceCount,
    IReadOnlyList<RefactorFile> Files,
    IReadOnlyList<RefactorDiagnostic> Diagnostics)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static RefactorManifest SuccessNoMove(string symbolName, string namespaceName)
    {
        var display = namespaceName.Length == 0 ? symbolName : namespaceName + "." + symbolName;
        return new(true, false, false, display, display, 0, [], []);
    }

    public static async Task WriteFailure(string path, string code, string message) =>
        await Write(path, new(false, false, false, null, null, 0, [], [new(code, message)]));

    public static async Task Write(string path, RefactorManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(manifest, JsonOptions),
            new UTF8Encoding(false));
    }
}
