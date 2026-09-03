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
    public void Rebase_is_identity_when_the_deterministic_projection_did_not_change()
    {
        const string baseline = "generated";
        const string human = "generated + human detail";

        var result = CSharpRebaser.Rebase(baseline, human, baseline);

        Assert.True(result.IsSuccess);
        Assert.Equal(human, result.Source);
    }
}
