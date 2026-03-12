//@ should_pass
// Ensure that SPIRE005 is NOT triggered when args+activationAttributes overload is called with EmptyMustInitStruct and null args.
public class NoReport_ArgsActivation_EmptyMustInitStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyMustInitStruct), (object[])null, (object[])null);
    }
}
