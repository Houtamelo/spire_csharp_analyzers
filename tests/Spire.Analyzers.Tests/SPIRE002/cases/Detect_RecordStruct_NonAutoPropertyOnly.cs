//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a record struct with only a non-auto expression-bodied property.
[EnforceInitialization] //~ ERROR
public record struct RecordStructNonAutoPropertyOnly
{
    public int Value => 42;
}
