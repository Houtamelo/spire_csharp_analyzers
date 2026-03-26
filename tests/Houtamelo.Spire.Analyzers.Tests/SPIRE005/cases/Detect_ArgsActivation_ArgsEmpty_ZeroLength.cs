//@ should_fail
// Ensure that SPIRE005 IS triggered when args+activationAttributes overload is called with zero-length args array.
public class Detect_ArgsActivation_ArgsEmpty_ZeroLength
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct), new object[0], (object[])null); //~ ERROR
    }
}
