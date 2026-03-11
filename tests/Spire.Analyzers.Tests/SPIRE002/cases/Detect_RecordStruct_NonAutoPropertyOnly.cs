//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a record struct with only a non-auto expression-bodied property.
[MustBeInit] //~ ERROR
public record struct RecordStructNonAutoPropertyOnly
{
    public int Value => 42;
}
