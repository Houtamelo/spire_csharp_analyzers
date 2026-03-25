//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.Resize with a record struct.
public class Detect_RecordStruct_ArrayResize
{
    public void Method()
    {
        EnforceInitializationRecordStruct[]? arr = null;
        Array.Resize(ref arr, 5); //~ ERROR
    }
}
