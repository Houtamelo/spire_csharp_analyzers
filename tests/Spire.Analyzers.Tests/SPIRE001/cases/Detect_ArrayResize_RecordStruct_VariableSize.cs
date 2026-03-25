//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.Resize with a record struct and a variable size.
public class Detect_ArrayResize_RecordStruct_VariableSize
{
    public void Method(int n)
    {
        EnforceInitializationRecordStruct[]? arr = null;
        Array.Resize(ref arr, n); //~ ERROR
    }
}
