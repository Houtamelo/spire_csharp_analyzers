//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a readonly ref struct with an empty body.
[MustBeInit] //~ ERROR
public readonly ref struct EmptyReadonlyRefStruct { }
