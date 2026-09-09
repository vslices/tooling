using VSlices.Vsir;
using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class ReviewLoweringCorpusTests
{
    [Fact]
    public void Review_corpus_is_complete_parseable_and_lowerable_with_pinned_production_ruleset()
    {
        var root = FindRepositoryRoot();
        var corpusRoot = Path.Combine(root, "review", "lowering-corpus");
        var artifactsRoot = Path.Combine(corpusRoot, "artifacts");
        var entries = ReadEntries(Path.Combine(corpusRoot, "corpus.tsv"));

        var declared = entries
            .Select(entry => entry.File)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var present = Directory
            .EnumerateFiles(artifactsRoot, "*.vsir", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(declared, present);
        Assert.Equal(declared.Length, declared.Distinct(StringComparer.Ordinal).Count());

        var loaded = CSharpLoweringRuleSet.Load(Path.Combine(corpusRoot, "ruleset"));
        Assert.True(loaded.IsSuccess, string.Join(Environment.NewLine, loaded.Diagnostics));

        foreach (var entry in entries)
        {
            var source = File.ReadAllText(Path.Combine(artifactsRoot, entry.File));
            var parsed = VsirParser.Parse(source);
            Assert.True(
                parsed.IsSuccess,
                $"{entry.File} did not parse:{Environment.NewLine}{string.Join(Environment.NewLine, parsed.Diagnostics)}");

            var context = new CSharpLoweringContext(entry.Namespace, loaded.RuleSet!);
            var document = parsed.Document!;
            var lowered = document.Classification switch
            {
                "maintained" => CSharpMaintainedDomainTypeLowerer.Lower(document, context),
                _ when document.Shape == "sum" => CSharpSumDomainTypeLowerer.Lower(document, context),
                _ => CSharpLanguageLowerer.Lower(document, context)
            };

            Assert.True(
                lowered.IsSuccess,
                $"{entry.File} did not lower with the pinned production Ruleset:{Environment.NewLine}{string.Join(Environment.NewLine, lowered.Diagnostics)}");
            Assert.False(string.IsNullOrWhiteSpace(lowered.Source));
            Assert.Contains(document.Name, lowered.Source!, StringComparison.Ordinal);
        }
    }

    private static IReadOnlyList<CorpusEntry> ReadEntries(string path)
    {
        var lines = File.ReadAllLines(path);
        Assert.NotEmpty(lines);
        Assert.Equal("file\tnamespace\torigin\tpurpose", lines[0]);

        return lines
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var columns = line.Split('\t');
                Assert.Equal(4, columns.Length);
                return new CorpusEntry(columns[0], columns[1], columns[2], columns[3]);
            })
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "tooling.slnx")))
                return current.FullName;
        }

        throw new InvalidOperationException("Could not locate tooling.slnx from test output.");
    }

    private sealed record CorpusEntry(string File, string Namespace, string Origin, string Purpose);
}
