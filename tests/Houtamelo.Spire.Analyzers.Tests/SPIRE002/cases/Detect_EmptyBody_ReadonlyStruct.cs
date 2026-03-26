//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a readonly struct with an empty body.
[EnforceInitialization] //~ ERROR
public readonly struct EmptyReadonlyStruct { }
