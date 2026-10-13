#nullable enable
using Houtamelo.Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    partial struct NullableReferenceAnnotations
    {
        [Variant] public static partial NullableReferenceAnnotations Required(string value);
        [Variant] public static partial NullableReferenceAnnotations Optional(string? value);
    }
}
