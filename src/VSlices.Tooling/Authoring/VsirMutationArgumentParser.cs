namespace VSlices.Tooling;

internal static class VsirMutationArgumentParser
{
    public static string? AddMutations(
        ICollection<VsirMutation> target,
        VsirMutationKind kind,
        string? clauses,
        string diagnosticCode)
    {
        if (string.IsNullOrWhiteSpace(clauses))
            return null;

        foreach (var clause in clauses.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = clause.IndexOf('=');
            if (separator < 0)
            {
                if (kind != VsirMutationKind.Remove || string.IsNullOrWhiteSpace(clause))
                    return $"{diagnosticCode}: Mutation '{clause}' must use path=value syntax.";

                target.Add(new(kind, clause.Trim(), null));
                continue;
            }

            if (separator == 0 || separator == clause.Length - 1)
                return $"{diagnosticCode}: Mutation '{clause}' must use non-empty path=value syntax.";

            var path = clause[..separator].Trim();
            var value = clause[(separator + 1)..].Trim();
            if (path.Length == 0 || value.Length == 0)
                return $"{diagnosticCode}: Mutation '{clause}' must use non-empty path=value syntax.";

            target.Add(new(kind, path, value));
        }

        return null;
    }

    public static string? AddRepeatedOptions(
        ICollection<VsirMutation> target,
        ReadOnlySpan<string> commandArguments,
        string diagnosticCode)
    {
        for (var index = 0; index < commandArguments.Length; index++)
        {
            var token = commandArguments[index];
            if (!TryMutationKind(token, out var kind))
                continue;

            if (index + 1 >= commandArguments.Length)
                return $"{diagnosticCode}: Option '{token}' requires a mutation value.";

            var value = commandArguments[++index];
            if (TryMutationKind(value, out _))
                return $"{diagnosticCode}: Option '{token}' requires a mutation value.";

            var error = AddMutations(target, kind, value, diagnosticCode);
            if (error is not null)
                return error;
        }

        return null;
    }

    private static bool TryMutationKind(string token, out VsirMutationKind kind)
    {
        switch (token)
        {
            case "--add":
                kind = VsirMutationKind.Add;
                return true;
            case "--remove":
                kind = VsirMutationKind.Remove;
                return true;
            case "--set":
                kind = VsirMutationKind.Set;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
