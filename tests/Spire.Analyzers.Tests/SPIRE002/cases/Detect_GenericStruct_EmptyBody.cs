//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a generic struct S<T> with no instance fields.
[MustBeInit] //~ ERROR
public struct S<T> { }
