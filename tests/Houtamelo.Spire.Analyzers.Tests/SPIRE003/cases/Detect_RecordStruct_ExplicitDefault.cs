//@ should_fail
// Ensure that SPIRE003 IS triggered when default(EnforceInitializationRecordStruct) is used in a local variable.
public class Detect_RecordStruct_ExplicitDefault
{
    public void Method()
    {
        var s = default(EnforceInitializationRecordStruct); //~ ERROR
    }
}
