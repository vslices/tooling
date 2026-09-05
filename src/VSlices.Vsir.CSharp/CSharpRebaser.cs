using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

public enum CSharpRebaseResolution
{
    None,
    Deterministic
}

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
        string nextDeterministicSource,
        CSharpRebaseResolution resolution = CSharpRebaseResolution.None)
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
            if (!TryLocateInsertionSlot(
                    previousDeterministicSource,
                    humanSource,
                    prefixLength,
                    out var humanInsertionStart,
                    out var humanInsertionLength))
            {
                return new(null, [new(
                    "REB002",
                    "The deterministic insertion point is missing or ambiguous in the human projection. " +
                    "VSlices could not establish a unique surrounding context, so no automatic edit was attempted.")]);
            }

            var humanInserted = humanSource.Substring(humanInsertionStart, humanInsertionLength);

            if (string.Equals(humanInserted, nextChanged, StringComparison.Ordinal))
                return new(humanSource, []);

            if (humanInserted.Length == 0)
                return new(humanSource.Insert(humanInsertionStart, nextChanged), []);

            if (resolution == CSharpRebaseResolution.Deterministic)
            {
                var resolved = humanSource
                    .Remove(humanInsertionStart, humanInsertionLength)
                    .Insert(humanInsertionStart, nextChanged);
                return new(resolved, []);
            }

            const string resolutionGuidance =
                "Resolve the human projection manually and rerun, or pass '--resolve deterministic' " +
                "to replace only this conflicting insertion with the deterministic change while preserving unrelated human edits.";

            return new(null, [CreateConflictDiagnostic(
                "REB004",
                "Human and deterministic projections both changed the same insertion point.",
                $"Baseline insertion:{Environment.NewLine}{DisplayFull(string.Empty)}{Environment.NewLine}{Environment.NewLine}" +
                $"Human insertion:{Environment.NewLine}{DisplayFull(humanInserted)}{Environment.NewLine}{Environment.NewLine}" +
                $"Next deterministic insertion:{Environment.NewLine}{DisplayFull(nextChanged)}{Environment.NewLine}{Environment.NewLine}" +
                resolutionGuidance,
                BuildTrace(
                    previousDeterministicSource,
                    humanSource,
                    nextDeterministicSource,
                    prefixLength,
                    suffixLength,
                    previousChanged,
                    nextChanged,
                    humanInsertionStart,
                    humanInsertionLength),
                $"  Baseline insertion: {DisplaySnippet(string.Empty)}" + Environment.NewLine +
                $"  Human insertion: {DisplaySnippet(humanInserted)}" + Environment.NewLine +
                $"  Next deterministic insertion: {DisplaySnippet(nextChanged)}" + Environment.NewLine +
                resolutionGuidance)]);
        }

        var directIndex = humanSource.IndexOf(previousChanged, StringComparison.Ordinal);
        if (directIndex < 0)
        {
            return new(null, [CreateConflictDiagnostic(
                "REB001",
                "The VSIR-generated region changed in the human projection and cannot be rebased deterministically.",
                $"Previous deterministic region:{Environment.NewLine}{DisplayFull(previousChanged)}{Environment.NewLine}{Environment.NewLine}" +
                $"Next deterministic region:{Environment.NewLine}{DisplayFull(nextChanged)}",
                BuildTrace(
                    previousDeterministicSource,
                    humanSource,
                    nextDeterministicSource,
                    prefixLength,
                    suffixLength,
                    previousChanged,
                    nextChanged),
                $"  Previous deterministic region: {DisplaySnippet(previousChanged)}" + Environment.NewLine +
                $"  Next deterministic region: {DisplaySnippet(nextChanged)}")]);
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
            return new(null, [CreateConflictDiagnostic(
                "REB002",
                "The VSIR-generated region is ambiguous in the human projection and cannot be rebased deterministically.",
                $"Previous deterministic region:{Environment.NewLine}{DisplayFull(previousChanged)}{Environment.NewLine}{Environment.NewLine}" +
                $"Next deterministic region:{Environment.NewLine}{DisplayFull(nextChanged)}",
                BuildTrace(
                    previousDeterministicSource,
                    humanSource,
                    nextDeterministicSource,
                    prefixLength,
                    suffixLength,
                    previousChanged,
                    nextChanged),
                $"  Previous deterministic region: {DisplaySnippet(previousChanged)}" + Environment.NewLine +
                $"  Next deterministic region: {DisplaySnippet(nextChanged)}")]);
        }

        var rebased = humanSource
            .Remove(contextualIndex, previousChanged.Length)
            .Insert(contextualIndex, nextChanged);
        return new(rebased, []);
    }

    private static VsirDiagnostic CreateConflictDiagnostic(
        string code,
        string message,
        string details,
        string trace,
        string compactDetails) =>
        new(code, message + Environment.NewLine + compactDetails, details, trace);

    private static string BuildTrace(
        string previousDeterministicSource,
        string humanSource,
        string nextDeterministicSource,
        int prefixLength,
        int suffixLength,
        string previousChanged,
        string nextChanged,
        int? humanInsertionStart = null,
        int? humanInsertionLength = null)
    {
        var insertion = humanInsertionStart is null
            ? string.Empty
            : Environment.NewLine +
              $"Human insertion start: {humanInsertionStart}{Environment.NewLine}" +
              $"Human insertion length: {humanInsertionLength}";

        return
            $"Common prefix length: {prefixLength}{Environment.NewLine}" +
            $"Common suffix length: {suffixLength}{Environment.NewLine}" +
            $"Previous changed length: {previousChanged.Length}{Environment.NewLine}" +
            $"Next changed length: {nextChanged.Length}" + insertion + Environment.NewLine + Environment.NewLine +
            $"Previous deterministic source:{Environment.NewLine}{DisplayFull(previousDeterministicSource)}{Environment.NewLine}{Environment.NewLine}" +
            $"Human source:{Environment.NewLine}{DisplayFull(humanSource)}{Environment.NewLine}{Environment.NewLine}" +
            $"Next deterministic source:{Environment.NewLine}{DisplayFull(nextDeterministicSource)}";
    }

    private static bool TryLocateInsertionSlot(
        string previousDeterministicSource,
        string humanSource,
        int insertionIndex,
        out int humanInsertionStart,
        out int humanInsertionLength)
    {
        humanInsertionStart = -1;
        humanInsertionLength = 0;

        var leftAvailable = insertionIndex;
        var rightAvailable = previousDeterministicSource.Length - insertionIndex;
        var maxContext = Math.Max(leftAvailable, rightAvailable);

        for (var context = 1; context <= maxContext; context++)
        {
            var leftLength = Math.Min(context, leftAvailable);
            var rightLength = Math.Min(context, rightAvailable);

            var leftAnchor = leftLength == 0
                ? string.Empty
                : previousDeterministicSource.Substring(insertionIndex - leftLength, leftLength);
            var rightAnchor = rightLength == 0
                ? string.Empty
                : previousDeterministicSource.Substring(insertionIndex, rightLength);

            if (!TryLocateExpectedUniqueAnchor(
                    previousDeterministicSource,
                    humanSource,
                    leftAnchor,
                    insertionIndex - leftLength,
                    out var humanLeftStart))
            {
                continue;
            }

            if (!TryLocateExpectedUniqueAnchor(
                    previousDeterministicSource,
                    humanSource,
                    rightAnchor,
                    insertionIndex,
                    out var humanRightStart))
            {
                continue;
            }

            var leftEnd = leftAnchor.Length == 0
                ? 0
                : humanLeftStart + leftAnchor.Length;
            var rightStart = rightAnchor.Length == 0
                ? humanSource.Length
                : humanRightStart;

            if (rightStart < leftEnd)
                continue;

            humanInsertionStart = leftEnd;
            humanInsertionLength = rightStart - leftEnd;
            return true;
        }

        return false;
    }

    private static bool TryLocateExpectedUniqueAnchor(
        string deterministicSource,
        string humanSource,
        string anchor,
        int expectedDeterministicIndex,
        out int humanIndex)
    {
        humanIndex = -1;
        if (anchor.Length == 0)
            return true;

        if (!HasSingleOccurrence(deterministicSource, anchor, out var deterministicIndex) ||
            deterministicIndex != expectedDeterministicIndex)
        {
            return false;
        }

        return HasSingleOccurrence(humanSource, anchor, out humanIndex);
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

    private static string DisplaySnippet(string value)
    {
        if (value.Length == 0)
            return "<empty>";

        var escaped = value
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
        const int maximum = 160;
        if (escaped.Length > maximum)
            escaped = escaped[..maximum] + "...";

        return $"'{escaped}'";
    }

    private static string DisplayFull(string value) =>
        value.Length == 0 ? "<empty>" : value;

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
