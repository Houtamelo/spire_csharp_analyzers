//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<EnforceInitializationRecordStruct>() is used on a [EnforceInitialization] record struct.
public class Detect_GenericOverload_RecordStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance<EnforceInitializationRecordStruct>(); //~ ERROR
    }
}
