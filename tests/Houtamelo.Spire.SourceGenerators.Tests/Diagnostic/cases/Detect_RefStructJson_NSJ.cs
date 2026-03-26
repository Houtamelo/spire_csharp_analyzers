//@ should_fail
// SPIRE_DU008: ref struct with NewtonsoftJson cannot use JSON generation
using Spire;
namespace TestNs
{
    [DiscriminatedUnion(Json = JsonLibrary.NewtonsoftJson)]
    ref partial struct SpanUnion //~ ERROR
    {
        [Variant] public static partial SpanUnion Alpha(int x);
        [Variant] public static partial SpanUnion Beta(double y);
    }
}
