//@ should_fail
// SPIRE_DU005: generic struct cannot use Overlap layout
using Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.Overlap)]
    partial struct Result<T> //~ ERROR
    {
        [Variant] public static partial Result<T> Ok(T value);
        [Variant] public static partial Result<T> Err(string message);
    }
}
