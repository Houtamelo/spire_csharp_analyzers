//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationRecordStruct)) is used on a [EnforceInitialization] record struct.
public class Detect_TypeOnly_RecordStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationRecordStruct)); //~ ERROR
    }
}
