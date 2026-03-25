//@ should_fail
// Ensure that SPIRE005 IS triggered when args+activationAttributes overload is called with a EnforceInitialization record struct and null args.
public class Detect_ArgsActivation_RecordStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationRecordStruct), (object[])null, (object[])null); //~ ERROR
    }
}
