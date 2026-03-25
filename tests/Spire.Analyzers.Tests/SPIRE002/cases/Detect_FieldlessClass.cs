//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is on a class with no fields.
[EnforceInitialization] //~ ERROR
public class EmptyEnforceInitializationClass { }
