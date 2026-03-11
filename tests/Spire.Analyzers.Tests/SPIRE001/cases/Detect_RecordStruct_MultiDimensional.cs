//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a multi-dimensional array of a record struct.
public class Detect_RecordStruct_MultiDimensional
{
    public void Method()
    {
        var arr = new MustInitRecordStruct[2, 3]; //~ ERROR
    }
}
