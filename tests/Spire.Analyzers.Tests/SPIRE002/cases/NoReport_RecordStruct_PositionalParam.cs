//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a record struct with a positional parameter.
[EnforceInitialization]
public record struct RecordStructPositionalParam(int X);
