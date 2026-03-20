//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is on a record class with properties.
[MustBeInit]
public record MustInitRecordWithField(int Value);
