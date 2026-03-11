//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a ref struct with an empty body.
[MustBeInit] //~ ERROR
public ref struct EmptyRefStruct { }
