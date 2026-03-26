//@ should_pass
// Ensure that SPIRE005 is NOT triggered when args+activationAttributes overload is called with a plain struct type and null args.
public class NoReport_ArgsActivation_PlainStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(PlainStruct), (object[])null, (object[])null);
    }
}
