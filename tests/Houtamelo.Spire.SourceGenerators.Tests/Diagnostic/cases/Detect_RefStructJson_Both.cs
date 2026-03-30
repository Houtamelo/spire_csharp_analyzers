//@ should_fail
// SPIRE_DU008: ref struct with both JSON libraries cannot use JSON generation
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion(json: JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson)]
    ref partial struct BufferUnion //~ ERROR
    {
        [Variant] public static partial BufferUnion Read(int length);
        [Variant] public static partial BufferUnion Write(byte[] data);
    }
}
