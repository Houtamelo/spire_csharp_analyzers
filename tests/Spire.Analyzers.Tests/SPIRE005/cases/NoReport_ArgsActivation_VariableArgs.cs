//@ should_pass
// Ensure that SPIRE005 is NOT triggered when args+activationAttributes overload is called with a variable as args.
public class NoReport_ArgsActivation_VariableArgs
{
    public void Method()
    {
        object[] args = new object[] { 42 };
        var x = Activator.CreateInstance(typeof(MustInitStruct), args, (object[])null);
    }
}
