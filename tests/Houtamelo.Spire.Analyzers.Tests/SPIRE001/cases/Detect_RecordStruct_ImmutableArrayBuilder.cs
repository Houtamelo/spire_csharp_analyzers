//@ should_fail
// Ensure that SPIRE001 IS triggered when setting ImmutableArray.Builder.Count with a record struct.
public class Detect_RecordStruct_ImmutableArrayBuilder
{
    public void Method()
    {
        var builder = ImmutableArray.CreateBuilder<EnforceInitializationRecordStruct>();
        builder.Count = 5; //~ ERROR
    }
}
