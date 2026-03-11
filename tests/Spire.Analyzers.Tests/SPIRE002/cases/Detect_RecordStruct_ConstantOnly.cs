//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a record struct with only a constant.
[MustBeInit] //~ ERROR
public record struct RecordStructConstantOnly
{
    public const int MaxValue = 100;
}
