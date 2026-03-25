//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is on a record class with no fields.
[EnforceInitialization] //~ ERROR
public record EmptyEnforceInitializationRecord;
