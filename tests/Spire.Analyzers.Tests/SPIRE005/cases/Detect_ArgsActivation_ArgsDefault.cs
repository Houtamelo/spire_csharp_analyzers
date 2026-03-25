//@ should_fail
// Ensure that SPIRE005 IS triggered when args+activationAttributes overload is called with default(object[]) args.
public class Detect_ArgsActivation_ArgsDefault
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct), default(object[]), (object[])null); //~ ERROR
    }
}
