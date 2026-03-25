//@ should_pass
// Ensure that SPIRE005 is NOT triggered when args+activationAttributes overload is called with EmptyEnforceInitializationStruct and null args.
public class NoReport_ArgsActivation_EmptyEnforceInitializationStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyEnforceInitializationStruct), (object[])null, (object[])null);
    }
}
