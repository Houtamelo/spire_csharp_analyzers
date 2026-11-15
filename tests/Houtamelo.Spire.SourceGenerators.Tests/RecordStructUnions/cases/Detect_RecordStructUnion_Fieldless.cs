//@ should_fail
// A fieldless struct-based union still cannot use the record keyword.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial record struct EmptyUnion //~ ERROR
{
}
