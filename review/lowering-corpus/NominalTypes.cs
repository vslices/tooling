namespace VSlices.Review.Corpus.Nominals;

// Target-context placeholders only. They let the Roslyn type-resolution helper
// establish concrete C# namespaces for nominal VSIR leaves during isolated
// review-corpus generation. They deliberately contain no domain behavior.
public sealed class Rut
{
    public sealed record Repr(string Value);
}

public sealed class CommuneId;

public sealed class Commune
{
    public sealed record Repr(string Id);
}

public sealed class Region
{
    public sealed record Repr;
}

public sealed class Province
{
    public sealed record Repr;
}

public sealed class StreetName
{
    public sealed record Repr(string Value);
}

public sealed class StreetExtension
{
    public sealed record Repr(string Value);
}

public sealed class Location
{
    public sealed record Repr;
}

public sealed class SrvIdentityId
{
    public sealed record Repr(string Value);
}

public sealed class FullName
{
    public sealed record Repr;
}

public sealed class CompanyName
{
    public sealed record Repr;
}

public sealed class TicketSearch
{
    public sealed record Repr;
}

public sealed class ProjectReference
{
    public sealed record Repr;
}

public sealed class IncidentTypeReference
{
    public sealed record Repr;
}

public sealed class AccountReference
{
    public sealed record Repr;
}

public sealed class TicketDateRange
{
    public sealed record Repr;
}
