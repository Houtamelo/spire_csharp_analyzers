//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a generic struct S<T> with no instance fields.
[EnforceInitialization] //~ ERROR
public struct S<T> { }
