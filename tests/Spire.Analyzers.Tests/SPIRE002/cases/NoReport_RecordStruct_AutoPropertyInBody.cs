//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a record struct with an auto-property in its body.
[MustBeInit]
public record struct RecordStructAutoPropertyInBody
{
    public int Value { get; set; }
}
