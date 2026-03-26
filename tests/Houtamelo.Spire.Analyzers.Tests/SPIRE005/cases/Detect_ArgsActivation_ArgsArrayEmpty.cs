//@ should_fail
// Ensure that SPIRE005 IS triggered when args+activationAttributes overload is called with Array.Empty<object>() args.
public class Detect_ArgsActivation_ArgsArrayEmpty
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct), Array.Empty<object>(), (object[])null); //~ ERROR
    }
}
