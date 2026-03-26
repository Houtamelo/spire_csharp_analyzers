//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a record struct with an auto-property in its body.
[EnforceInitialization]
public record struct RecordStructAutoPropertyInBody
{
    public int Value { get; set; }
}
