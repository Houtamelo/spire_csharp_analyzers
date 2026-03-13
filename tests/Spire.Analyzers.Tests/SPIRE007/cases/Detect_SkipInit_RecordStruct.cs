//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit is called with a MustInitRecordStruct.
public class Detect_SkipInit_RecordStruct
{
    public void Method()
    {
        Unsafe.SkipInit(out MustInitRecordStruct s); //~ ERROR
    }
}
