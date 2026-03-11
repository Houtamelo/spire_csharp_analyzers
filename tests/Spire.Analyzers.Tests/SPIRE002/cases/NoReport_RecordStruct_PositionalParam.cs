//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a record struct with a positional parameter.
[MustBeInit]
public record struct RecordStructPositionalParam(int X);
