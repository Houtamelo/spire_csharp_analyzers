//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is on a record class with properties.
[EnforceInitialization]
public record EnforceInitializationRecordWithField(int Value);
