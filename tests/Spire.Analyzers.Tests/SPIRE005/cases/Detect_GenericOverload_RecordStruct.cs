//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitRecordStruct>() is used on a [MustBeInit] record struct.
public class Detect_GenericOverload_RecordStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance<MustInitRecordStruct>(); //~ ERROR
    }
}
