//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.CreateInstance with a record struct.
public class Detect_RecordStruct_ArrayCreateInstance
{
    public void Method()
    {
        var arr = Array.CreateInstance(typeof(EnforceInitializationRecordStruct), 5); //~ ERROR
    }
}
