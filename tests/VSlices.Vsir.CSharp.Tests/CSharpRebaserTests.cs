using VSlices.Vsir.CSharp;

namespace VSlices.Vsir.CSharp.Tests;

public sealed class CSharpRebaserTests
{
    [Fact]
    public void Rebase_preserves_human_changes_outside_the_generated_delta()
    {
        const string previous = """
            class StreetName
            {
                // generated invariant
                MaxLength = 30;

                // generated representation
                To();
            }
            """;

        const string human = """
            class StreetName
            {
                // generated invariant
                MaxLength = 30;

                // human optimization
                CachedLookup();

                // generated representation
                To();
            }
            """;

        const string next = """
            class StreetName
            {
                // generated invariant
                MaxLength = 40;

                // generated representation
                To();
            }
            """;

        var result = CSharpRebaser.Rebase(previous, human, next);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Contains("MaxLength = 40;", result.Source);
        Assert.Contains("CachedLookup();", result.Source);
    }

    [Fact]
    public void Rebase_uses_unique_deterministic_context_when_the_minimal_delta_is_ambiguous()
    {
        const string previous = """
            class StreetName
            {
                Other = 10;
                MaxLength = 30;
                Another = 20;
            }
            """;

        const string human = """
            class StreetName
            {
                Other = 10;
                MaxLength = 30;
                Another = 20;

                // human detail
            }
            """;

        const string next = """
            class StreetName
            {
                Other = 10;
                MaxLength = 31;
                Another = 20;
            }
            """;

        var result = CSharpRebaser.Rebase(previous, human, next);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Contains("MaxLength = 31;", result.Source, StringComparison.Ordinal);
        Assert.Contains("Other = 10;", result.Source, StringComparison.Ordinal);
        Assert.Contains("Another = 20;", result.Source, StringComparison.Ordinal);
        Assert.Contains("// human detail", result.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Rebase_remains_ambiguous_when_even_full_deterministic_context_occurs_twice()
    {
        const string previous = "value=30;";
        const string human = """
            value=30;
            value=30;
            """;
        const string next = "value=31;";

        var result = CSharpRebaser.Rebase(previous, human, next);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, x => x.Code == "REB002");
    }

    [Fact]
    public void Rebase_rejects_a_conflict_when_the_human_changed_the_same_region()
    {
        const string previous = "MaxLength = 30;";
        const string human = "MaxLength = GetConfiguredMaximum();";
        const string next = "MaxLength = 40;";

        var result = CSharpRebaser.Rebase(previous, human, next);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, x => x.Code == "REB001");
        Assert.Equal("MaxLength = GetConfiguredMaximum();", human);
    }

    [Fact]
    public void Rebase_conflict_preserves_full_details_and_trace_beyond_compact_preview()
    {
        var previousRegion = new string('a', 220) + "PREVIOUS-END";
        var nextRegion = new string('b', 220) + "NEXT-END";
        var human = new string('c', 220) + "HUMAN-END";

        var result = CSharpRebaser.Rebase(previousRegion, human, nextRegion);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("REB001", diagnostic.Code);
        Assert.Contains("...", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("PREVIOUS-END", diagnostic.Details, StringComparison.Ordinal);
        Assert.Contains("NEXT-END", diagnostic.Details, StringComparison.Ordinal);
        Assert.Contains("PREVIOUS-END", diagnostic.Trace, StringComparison.Ordinal);
        Assert.Contains("HUMAN-END", diagnostic.Trace, StringComparison.Ordinal);
        Assert.Contains("NEXT-END", diagnostic.Trace, StringComparison.Ordinal);
        Assert.Contains("Common prefix length:", diagnostic.Trace, StringComparison.Ordinal);
    }

    [Fact]
    public void Rebase_reports_baseline_human_and_next_for_a_concurrent_insertion()
    {
        const string previous = """
            namespace Tickets.Domain;

            public sealed class TicketCode;
            """;
        const string human = """
            using static Something;

            namespace Tickets.Domain.Aggregates;

            public sealed class TicketCode;

            // human detail
            """;
        const string next = """
            namespace Tickets.Domain.Aggregates.Tickets;

            public sealed class TicketCode;
            """;

        var result = CSharpRebaser.Rebase(previous, human, next);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("REB004", diagnostic.Code);
        Assert.Contains("Baseline insertion: <empty>", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("Human insertion: '.Aggregates'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("Next deterministic insertion: '.Aggregates.Tickets'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("--resolve deterministic", diagnostic.Details, StringComparison.Ordinal);
    }

    [Fact]
    public void Rebase_can_resolve_only_the_conflicting_insertion_deterministically()
    {
        const string previous = """
            namespace Tickets.Domain;

            public sealed class TicketCode;
            """;
        const string human = """
            using static Something;

            namespace Tickets.Domain.Aggregates;

            public sealed class TicketCode;

            // human detail
            """;
        const string next = """
            namespace Tickets.Domain.Aggregates.Tickets;

            public sealed class TicketCode;
            """;

        var result = CSharpRebaser.Rebase(
            previous,
            human,
            next,
            CSharpRebaseResolution.Deterministic);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Contains("namespace Tickets.Domain.Aggregates.Tickets;", result.Source, StringComparison.Ordinal);
        Assert.Contains("using static Something;", result.Source, StringComparison.Ordinal);
        Assert.Contains("// human detail", result.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Tickets.Domain.Aggregates;", result.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Rebase_accepts_an_insertion_that_the_human_projection_already_matches()
    {
        const string previous = "namespace Tickets.Domain;\nclass TicketCode;";
        const string human = "namespace Tickets.Domain.Aggregates.Tickets;\nclass TicketCode;\n// human detail";
        const string next = "namespace Tickets.Domain.Aggregates.Tickets;\nclass TicketCode;";

        var result = CSharpRebaser.Rebase(previous, human, next);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(human, result.Source);
    }

    [Fact]
    public void Rebase_is_identity_when_the_deterministic_projection_did_not_change()
    {
        const string baseline = "generated";
        const string human = "generated + human detail";

        var result = CSharpRebaser.Rebase(baseline, human, baseline);

        Assert.True(result.IsSuccess);
        Assert.Equal(human, result.Source);
    }
}
