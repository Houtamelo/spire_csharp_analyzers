using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

/// Classifies fields into three overlap regions:
/// Unmanaged (primitives with known sizeof), Reference (pointer-sized), Boxed (everything else).
internal static class FieldClassifier
{
    public static FieldRegion Classify(FieldInfo field)
    {
        if (field.IsReferenceType) return FieldRegion.Reference;
        if (field.IsUnmanaged && field.KnownSize.HasValue) return FieldRegion.Unmanaged;
        return FieldRegion.Boxed;
    }
}
