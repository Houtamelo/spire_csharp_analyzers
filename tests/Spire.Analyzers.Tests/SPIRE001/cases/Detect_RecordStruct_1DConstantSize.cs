//@ should_fail
// Ensure that SPIRE001 IS triggered when creating an array of a record struct.
public class Detect_RecordStruct_1DConstantSize
{
    public void Method()
    {
        var arr = new EnforceInitializationRecordStruct[5]; //~ ERROR
    }
}
