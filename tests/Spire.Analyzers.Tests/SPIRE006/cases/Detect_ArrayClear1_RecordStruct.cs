//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) is called with a EnforceInitializationRecordStruct[] argument.
public class Detect_ArrayClear1_RecordStruct
{
    public void Method()
    {
        var arr = new EnforceInitializationRecordStruct[5];
        Array.Clear(arr); //~ ERROR
    }
}
