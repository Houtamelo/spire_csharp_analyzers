//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitRecordStruct), nonPublic: true) is called.
public class Detect_NonPublic_RecordStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitRecordStruct), true); //~ ERROR
    }
}
