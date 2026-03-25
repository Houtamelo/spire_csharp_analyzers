//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a ref struct with an empty body.
[EnforceInitialization] //~ ERROR
public ref struct EmptyRefStruct { }
