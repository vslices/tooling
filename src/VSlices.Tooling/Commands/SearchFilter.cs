using YamlDotNet.RepresentationModel;

namespace VSlices.Tooling;

internal sealed record SearchFilter(
    string Property,
    string Operator,
    string Value)
{
    public static (SearchFilter? Filter, string? Error) Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (null, "SEARCH001: --filter requires <property>:<operator>:<value>.");

        var first = value.IndexOf(':');
        var second = first < 0 ? -1 : value.IndexOf(':', first + 1);
        if (first <= 0 || second <= first + 1 || second == value.Length - 1)
            return (null, $"SEARCH001: Filter '{value}' must use <property>:<operator>:<value> syntax.");

        var property = value[..first].Trim();
        var @operator = value[(first + 1)..second].Trim();
        var filterValue = value[(second + 1)..].Trim();

        if (property.Length == 0 || @operator.Length == 0 || filterValue.Length == 0)
            return (null, $"SEARCH001: Filter '{value}' must use non-empty <property>:<operator>:<value> parts.");

        if (@operator is not ("contains" or "equals"))
            return (null, $"SEARCH002: Unsupported filter operator '{@operator}'. Supported operators: contains, equals.");

        return (new(property, @operator, filterValue), null);
    }

    public bool Matches(string source)
    {
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(source));
            if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
                return false;

            if (!root.Children.TryGetValue(new YamlScalarNode(Property), out var node))
                return false;

            return Operator switch
            {
                "equals" => MatchesEquals(node),
                "contains" => MatchesContains(node),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    private bool MatchesEquals(YamlNode node) =>
        node is YamlScalarNode scalar &&
        string.Equals(scalar.Value, Value, StringComparison.Ordinal);

    private bool MatchesContains(YamlNode node) =>
        node switch
        {
            YamlScalarNode scalar => scalar.Value?.Contains(Value, StringComparison.Ordinal) == true,
            YamlSequenceNode sequence => sequence.Children
                .OfType<YamlScalarNode>()
                .Any(item => string.Equals(item.Value, Value, StringComparison.Ordinal)),
            _ => false
        };
}
