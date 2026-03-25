//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a record struct with only a constant.
[EnforceInitialization] //~ ERROR
public record struct RecordStructConstantOnly
{
    public const int MaxValue = 100;
}
