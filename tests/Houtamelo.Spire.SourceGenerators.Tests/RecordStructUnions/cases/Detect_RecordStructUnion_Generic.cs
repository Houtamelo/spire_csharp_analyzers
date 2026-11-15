//@ should_fail
// A generic struct-based union still cannot be a record struct.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial record struct GenericUnion<T> //~ ERROR
    where T : unmanaged
{
    [Variant] public static partial GenericUnion<T> Value(T amount);
}
