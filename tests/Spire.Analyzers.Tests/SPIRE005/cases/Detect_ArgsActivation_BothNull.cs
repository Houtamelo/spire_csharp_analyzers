//@ should_fail
// Ensure that SPIRE005 IS triggered when args+activationAttributes overload is called with both args null.
public class Detect_ArgsActivation_BothNull
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), (object[])null, (object[])null); //~ ERROR
    }
}
