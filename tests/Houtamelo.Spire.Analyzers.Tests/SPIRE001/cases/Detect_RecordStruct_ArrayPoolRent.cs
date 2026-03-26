//@ should_fail
// Ensure that SPIRE001 IS triggered when calling ArrayPool.Rent with a record struct.
public class Detect_RecordStruct_ArrayPoolRent
{
    public void Method()
    {
        var arr = ArrayPool<EnforceInitializationRecordStruct>.Shared.Rent(5); //~ ERROR
    }
}
