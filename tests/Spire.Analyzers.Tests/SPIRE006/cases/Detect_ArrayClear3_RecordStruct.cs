//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr, 0, n) is called with a MustInitRecordStruct[] argument.
public class Detect_ArrayClear3_RecordStruct
{
    public void Method()
    {
        var arr = new MustInitRecordStruct[5];
        Array.Clear(arr, 0, arr.Length); //~ ERROR
    }
}
