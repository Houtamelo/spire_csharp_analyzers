//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a record struct with an explicit instance field in its body.
[EnforceInitialization]
public record struct RecordStructExplicitInstanceField
{
    public int Value;
}
