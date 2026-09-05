namespace VSlices.Vsir;

public sealed record DomainTypeVsir(
    string Version,
    string Kind,
    string Name,
    string Classification,
    string Shape,
    IReadOnlyList<string> Traits,
    string? RefinedFrom,
    ProductShape State,
    ProductShape Representation,
    RepresentationMapping? RepresentationMapping,
    Construction Construction,
    EqualitySemantics? Equality);

public sealed record ProductShape(IReadOnlyList<Field> Fields);
public sealed record Field(string Name, string Type);

public sealed record ConstructionInput(
    IReadOnlyList<Field> Fields,
    string? ScalarType)
{
    public bool IsScalar => !string.IsNullOrWhiteSpace(ScalarType);

    public static ConstructionInput Product(IReadOnlyList<Field> fields) =>
        new(fields, null);

    public static ConstructionInput Scalar(string type) =>
        new([], type);
}

public sealed record Construction(ConstructionInput Input, IReadOnlyList<ConstructionStep> Steps);

public sealed record EqualitySemantics(
    string? Intrinsic,
    string? Over,
    string By);

public sealed record RepresentationMapping(
    IReadOnlyDictionary<string, RepresentationProjection> Fields);

public abstract record RepresentationProjection;
public sealed record StringifyProjection(string Value) : RepresentationProjection;

public abstract record ConstructionStep;
public sealed record NormalizeStep(string Target, string Intrinsic) : ConstructionStep;
public sealed record EnsureStep(Condition Condition, string FailureMessage) : ConstructionStep;
public sealed record RefineStep(string Value, string As) : ConstructionStep;

public abstract record Condition;
public sealed record NonEmptyCondition(string Value) : Condition;
public sealed record NotWhitespaceCondition(string Value) : Condition;
public sealed record LengthAtMostCondition(string Value, int Max) : Condition;
public sealed record VsirDiagnostic(string Code, string Message);

public sealed record VsirSemanticExtensions(IReadOnlySet<string> NormalizeIntrinsics)
{
    public static VsirSemanticExtensions None { get; } =
        new(new HashSet<string>(StringComparer.Ordinal));

    public bool DeclaresNormalize(string intrinsic) =>
        NormalizeIntrinsics.Contains(intrinsic);
}

public sealed record VsirValidationContext(VsirSemanticExtensions SemanticExtensions)
{
    public static VsirValidationContext Empty { get; } =
        new(VsirSemanticExtensions.None);
}

public sealed record VsirParseResult(
    DomainTypeVsir? Document,
    IReadOnlyList<VsirDiagnostic> Diagnostics)
{
    public bool IsSuccess => Document is not null && Diagnostics.Count == 0;
}
