//@ should_fail
// Ensure that SPIRE005 IS triggered when args+activationAttributes overload is called with a MustBeInit record struct and null args.
public class Detect_ArgsActivation_RecordStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitRecordStruct), (object[])null, (object[])null); //~ ERROR
    }
}
