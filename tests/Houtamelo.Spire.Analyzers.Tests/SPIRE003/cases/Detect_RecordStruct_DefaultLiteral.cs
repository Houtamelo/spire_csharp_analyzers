//@ should_fail
// Ensure that SPIRE003 IS triggered when EnforceInitializationRecordStruct s = default; is used.
public class Detect_RecordStruct_DefaultLiteral
{
    public void Method()
    {
        EnforceInitializationRecordStruct s = default; //~ ERROR
    }
}
