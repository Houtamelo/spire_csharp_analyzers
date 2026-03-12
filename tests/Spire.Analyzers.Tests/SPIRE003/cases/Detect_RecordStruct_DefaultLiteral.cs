//@ should_fail
// Ensure that SPIRE003 IS triggered when MustInitRecordStruct s = default; is used.
public class Detect_RecordStruct_DefaultLiteral
{
    public void Method()
    {
        MustInitRecordStruct s = default; //~ ERROR
    }
}
