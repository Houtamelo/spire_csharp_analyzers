//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to an empty record struct with no positional parameters (semicolon form).
[EnforceInitialization] //~ ERROR
public record struct EmptyRecordStruct;
