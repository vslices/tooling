using VSlices.Vsir;

namespace VSlices.Vsir.CSharp;

public sealed record CSharpLoweringContext(
    string Namespace,
    CSharpLoweringRuleSet Rules,
    VsirValidationContext? ValidationContext = null);

public sealed record CSharpLoweringResult(
    string? Source,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess => Source is not null && Diagnostics.Count == 0;
}
