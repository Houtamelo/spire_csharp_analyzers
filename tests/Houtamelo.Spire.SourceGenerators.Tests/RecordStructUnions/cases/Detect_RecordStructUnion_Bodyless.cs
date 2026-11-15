//@ should_fail
// A bodyless record-struct union is still an invalid struct-based union.
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial record struct BodylessUnion; //~ ERROR
