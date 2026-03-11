//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to an empty record struct with no positional parameters (semicolon form).
[MustBeInit] //~ ERROR
public record struct EmptyRecordStruct;
