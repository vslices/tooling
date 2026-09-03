using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

public sealed record CSharpRebaseResult(
    string? Source,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess => Source is not null && Diagnostics.Count == 0;
}

public static class CSharpRebaser
{
    public static CSharpRebaseResult Rebase(
        string previousDeterministicSource,
        string humanSource,
        string nextDeterministicSource)
    {
        if (previousDeterministicSource == nextDeterministicSource)
            return new(humanSource, []);

        var prefixLength = CommonPrefixLength(previousDeterministicSource, nextDeterministicSource);
        var suffixLength = CommonSuffixLength(previousDeterministicSource, nextDeterministicSource, prefixLength);
        var previousChangedLength = previousDeterministicSource.Length - prefixLength - suffixLength;
        var nextChangedLength = nextDeterministicSource.Length - prefixLength - suffixLength;
        var previousChanged = previousDeterministicSource.Substring(prefixLength, previousChangedLength);
        var nextChanged = nextDeterministicSource.Substring(prefixLength, nextChangedLength);

        if (previousChanged.Length == 0)
        {
            var anchor = suffixLength > 0 ? previousDeterministicSource[^suffixLength..] : string.Empty;
            if (anchor.Length == 0)
                return new(null, [new("REB003", "Cannot establish a deterministic insertion anchor.")]);

            var anchorIndex = humanSource.IndexOf(anchor, StringComparison.Ordinal);
            if (anchorIndex < 0 || humanSource.IndexOf(anchor, anchorIndex + 1, StringComparison.Ordinal) >= 0)
                return new(null, [new("REB002", "Deterministic insertion anchor is missing or ambiguous in the human projection.")]);

            return new(humanSource.Insert(anchorIndex, nextChanged), []);
        }

        var index = humanSource.IndexOf(previousChanged, StringComparison.Ordinal);
        if (index < 0)
        {
            return new(null, [new(
                "REB001",
                "The VSIR-generated region changed by the developer and cannot be rebased deterministically.")]);
        }

        if (humanSource.IndexOf(previousChanged, index + previousChanged.Length, StringComparison.Ordinal) >= 0)
        {
            return new(null, [new(
                "REB002",
                "The VSIR-generated region is ambiguous in the human projection and cannot be rebased deterministically.")]);
        }

        var rebased = humanSource.Remove(index, previousChanged.Length).Insert(index, nextChanged);
        return new(rebased, []);
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
}
