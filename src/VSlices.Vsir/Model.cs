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
public sealed record Field(string Name, VsirType Type, string? From = null);

public abstract record VsirType
{
    public abstract string Display { get; }
    public override string ToString() => Display;
}

public sealed record NamedVsirType(string Name) : VsirType
{
    public override string Display => Name;
}

public sealed record UnaryVsirType(string Constructor, VsirType Value) : VsirType
{
    public override string Display => $"{Constructor}<{Value.Display}>";
}

public sealed record ConstructionInput(
    IReadOnlyList<Field> Fields,
    VsirType? ScalarType)
{
    public bool IsScalar => ScalarType is not null;

    public static ConstructionInput Product(IReadOnlyList<Field> fields) =>
        new(fields, null);

    public static ConstructionInput Scalar(VsirType type) =>
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
public sealed record ReferenceProjection(string Value) : RepresentationProjection;
public sealed record StringifyProjection(string Value) : RepresentationProjection;
public sealed record RepresentProjection(RepresentationProjection Value) : RepresentationProjection;
public sealed record SelectProjection(RepresentationProjection Source, string Field) : RepresentationProjection;
public sealed record MapProjection(
    RepresentationProjection Source,
    string Bind,
    RepresentationProjection Value) : RepresentationProjection;
public sealed record IntrinsicProjection(
    string Intrinsic,
    IReadOnlyList<RepresentationProjection> Values) : RepresentationProjection;

public abstract record SemanticExpression;
public sealed record SemanticReferenceExpression(string Value) : SemanticExpression;
public sealed record SemanticIntrinsicExpression(
    string Intrinsic,
    IReadOnlyList<SemanticExpression> Values) : SemanticExpression;

public abstract record ConstructionStep;
public sealed record NormalizeStep(string Target, string Intrinsic) : ConstructionStep;
public sealed record EnsureStep(Condition Condition, string FailureMessage) : ConstructionStep;
public sealed record ResolveStep(
    string Source,
    string Id,
    string As,
    string FailureMessage) : ConstructionStep;
public sealed record ApplyStep(string Over, ApplyInput Input, string As) : ConstructionStep;
public sealed record IntrinsicRefineStep(
    string Intrinsic,
    string Value,
    IReadOnlyDictionary<string, string> As,
    string FailureMessage) : ConstructionStep;
public sealed record RefineStep(string Value, string As) : ConstructionStep;

public abstract record ApplyInput;
public sealed record DirectApplyInput(
    IReadOnlyDictionary<string, string> Fields) : ApplyInput;
public sealed record MappedApplyInput(
    string Source,
    IReadOnlyDictionary<string, string> Map) : ApplyInput;

public abstract record Condition;
public sealed record NonEmptyCondition(SemanticExpression Value) : Condition
{
    public NonEmptyCondition(string value) : this(new SemanticReferenceExpression(value)) { }
}
public sealed record NotWhitespaceCondition(SemanticExpression Value) : Condition
{
    public NotWhitespaceCondition(string value) : this(new SemanticReferenceExpression(value)) { }
}
public sealed record LengthAtMostCondition(SemanticExpression Value, int Max) : Condition
{
    public LengthAtMostCondition(string value, int max) : this(new SemanticReferenceExpression(value), max) { }
}
public sealed record LengthBetweenCondition(SemanticExpression Value, int Min, int Max) : Condition
{
    public LengthBetweenCondition(string value, int min, int max) : this(new SemanticReferenceExpression(value), min, max) { }
}
public sealed record VsirDiagnostic(
    string Code,
    string Message,
    string? Details = null,
    string? Trace = null);

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
