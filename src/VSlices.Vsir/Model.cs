namespace VSlices.Vsir;

public sealed record DomainTypeVsir(
    string Version,
    string Kind,
    string Name,
    string Classification,
    string Shape,
    IReadOnlyList<string> Traits,
    ProductShape State,
    ProductShape Representation,
    Construction Construction,
    EqualitySemantics? Equality);

public sealed record ProductShape(IReadOnlyList<Field> Fields);
public sealed record Field(string Name, string Type);
public sealed record Construction(ProductShape Input, IReadOnlyList<ConstructionStep> Steps);
public sealed record EqualitySemantics(string Intrinsic, string By);
public abstract record ConstructionStep;
public sealed record NormalizeStep(string Target, string Intrinsic) : ConstructionStep;
public sealed record EnsureStep(Condition Condition, string FailureMessage) : ConstructionStep;
public abstract record Condition;
public sealed record NonEmptyCondition(string Value) : Condition;
public sealed record LengthAtMostCondition(string Value, int Max) : Condition;
public sealed record VsirDiagnostic(string Code, string Message);

public sealed record VsirParseResult(
    DomainTypeVsir? Document,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess => Document is not null && Diagnostics.Count == 0;
}
