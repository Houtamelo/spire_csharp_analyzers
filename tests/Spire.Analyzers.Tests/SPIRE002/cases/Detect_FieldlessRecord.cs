//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is on a record class with no fields.
[MustBeInit] //~ ERROR
public record EmptyMustInitRecord;
