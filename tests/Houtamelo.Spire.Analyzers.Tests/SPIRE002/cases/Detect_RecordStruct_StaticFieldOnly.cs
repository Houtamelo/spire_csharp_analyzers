//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a record struct with only a static field.
[EnforceInitialization] //~ ERROR
public record struct RecordStructStaticFieldOnly
{
    public static int StaticValue;
}
