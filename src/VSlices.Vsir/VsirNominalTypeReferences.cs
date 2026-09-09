namespace VSlices.Vsir;

/// <summary>
/// Enumerates nominal semantic type dependencies from every admitted model
/// position. Target adapters decide which names require target-native resolution;
/// this traversal only prevents model growth from silently creating blind spots.
/// </summary>
public static class VsirNominalTypeReferences
{
    public static IReadOnlyList<string> Enumerate(DomainTypeVsir document)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        AddName(document.RefinedFrom);
        AddName(document.Equality?.Over);
        if (document.Identity is not null)
            AddType(document.Identity.Type);

        AddShape(document.State);
        AddShape(document.Representation);
        AddConstruction(document.Construction);

        foreach (var variant in document.Variants ?? [])
        {
            AddName(variant.RefinedFrom);
            AddName(variant.Equality?.Over);
            AddShape(variant.State);
            AddShape(variant.Representation);
            AddConstruction(variant.Construction);
        }

        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();

        void AddShape(ProductShape shape)
        {
            foreach (var field in shape.Fields)
                AddType(field.Type);
        }

        void AddConstruction(Construction construction)
        {
            if (construction.Input.IsScalar)
                AddType(construction.Input.ScalarType);
            else
                foreach (var field in construction.Input.Fields)
                    AddType(field.Type);

            foreach (var step in construction.Steps)
            {
                switch (step)
                {
                    case ResolveStep resolve:
                        AddName(resolve.Source);
                        break;
                    case ApplyStep apply:
                        AddName(apply.Over);
                        break;
                }
            }
        }

        void AddType(VsirType? type)
        {
            switch (type)
            {
                case NamedVsirType named:
                    AddName(named.Name);
                    break;
                case UnaryVsirType unary:
                    AddType(unary.Value);
                    break;
            }
        }

        void AddName(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                names.Add(name);
        }
    }
}
