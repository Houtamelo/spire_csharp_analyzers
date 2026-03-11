//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a plain struct with an empty body.
[MustBeInit] //~ ERROR
public struct EmptyStruct { }
