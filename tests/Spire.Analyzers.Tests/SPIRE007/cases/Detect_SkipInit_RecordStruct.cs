//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit is called with a EnforceInitializationRecordStruct.
public class Detect_SkipInit_RecordStruct
{
    public void Method()
    {
        Unsafe.SkipInit(out EnforceInitializationRecordStruct s); //~ ERROR
    }
}
