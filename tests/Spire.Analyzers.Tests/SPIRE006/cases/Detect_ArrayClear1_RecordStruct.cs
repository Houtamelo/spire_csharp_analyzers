//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) is called with a MustInitRecordStruct[] argument.
public class Detect_ArrayClear1_RecordStruct
{
    public void Method()
    {
        var arr = new MustInitRecordStruct[5];
        Array.Clear(arr); //~ ERROR
    }
}
