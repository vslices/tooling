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

        var directIndex = humanSource.IndexOf(previousChanged, StringComparison.Ordinal);
        if (directIndex < 0)
        {
            return new(null, [new(
                "REB001",
                "The VSIR-generated region changed by the developer and cannot be rebased deterministically.")]);
        }

        if (humanSource.IndexOf(previousChanged, directIndex + previousChanged.Length, StringComparison.Ordinal) < 0)
        {
            var rebasedDirectly = humanSource
                .Remove(directIndex, previousChanged.Length)
                .Insert(directIndex, nextChanged);
            return new(rebasedDirectly, []);
        }

        if (!TryLocateWithDeterministicContext(
                previousDeterministicSource,
                humanSource,
                prefixLength,
                previousChanged.Length,
                out var contextualIndex))
        {
            return new(null, [new(
                "REB002",
                "The VSIR-generated region is ambiguous in the human projection and cannot be rebased deterministically.")]);
        }

        var rebased = humanSource
            .Remove(contextualIndex, previousChanged.Length)
            .Insert(contextualIndex, nextChanged);
        return new(rebased, []);
    }

    private static bool TryLocateWithDeterministicContext(
        string previousDeterministicSource,
        string humanSource,
        int changedStart,
        int changedLength,
        out int humanChangedStart)
    {
        humanChangedStart = -1;

        var changedEnd = changedStart + changedLength;
        var maxContext = Math.Max(changedStart, previousDeterministicSource.Length - changedEnd);

        for (var context = 1; context <= maxContext; context = NextContextSize(context, maxContext))
        {
            var windowStart = Math.Max(0, changedStart - context);
            var windowEnd = Math.Min(previousDeterministicSource.Length, changedEnd + context);
            var window = previousDeterministicSource[windowStart..windowEnd];

            if (!HasSingleOccurrence(previousDeterministicSource, window, out var deterministicIndex) ||
                deterministicIndex != windowStart)
            {
                if (context == maxContext)
                    break;
                continue;
            }

            if (!HasSingleOccurrence(humanSource, window, out var humanWindowStart))
            {
                if (context == maxContext)
                    break;
                continue;
            }

            humanChangedStart = humanWindowStart + (changedStart - windowStart);
            return true;
        }

        return false;
    }

    private static int NextContextSize(int current, int max) =>
        current >= max
            ? max + 1
            : Math.Min(max, current < 16 ? current + 1 : current * 2);

    private static bool HasSingleOccurrence(string source, string value, out int index)
    {
        index = source.IndexOf(value, StringComparison.Ordinal);
        if (index < 0)
            return false;

        return source.IndexOf(value, index + 1, StringComparison.Ordinal) < 0;
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
