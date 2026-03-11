//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a record struct with only a static field.
[MustBeInit] //~ ERROR
public record struct RecordStructStaticFieldOnly
{
    public static int StaticValue;
}
