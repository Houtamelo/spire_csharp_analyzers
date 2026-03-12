//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance args is (object[])null for a record struct.
public class Detect_Params_RecordStruct_NullArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitRecordStruct), (object[])null); //~ ERROR
    }
}
