namespace VSlices.Vsir;

/// <summary>
/// Canonical VSIR parser entry point.
/// VSIR 0.1 has one admitted grammar and no compatibility dispatch by document shape.
/// </summary>
public static class VsirParser
{
    public static VsirParseResult Parse(
        string text,
        VsirValidationContext? validationContext = null) =>
        VsirLanguageParser.Parse(text, validationContext);
}
