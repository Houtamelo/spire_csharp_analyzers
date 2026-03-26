//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationRecordStruct), nonPublic: true) is called.
public class Detect_NonPublic_RecordStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationRecordStruct), true); //~ ERROR
    }
}
