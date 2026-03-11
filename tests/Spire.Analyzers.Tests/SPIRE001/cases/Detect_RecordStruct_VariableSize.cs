//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a variable-size array of a record struct.
public class Detect_RecordStruct_VariableSize
{
    public void Method(int n)
    {
        var arr = new MustInitRecordStruct[n]; //~ ERROR
    }
}
