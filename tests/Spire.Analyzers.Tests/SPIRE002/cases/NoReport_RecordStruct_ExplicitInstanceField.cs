//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a record struct with an explicit instance field in its body.
[MustBeInit]
public record struct RecordStructExplicitInstanceField
{
    public int Value;
}
