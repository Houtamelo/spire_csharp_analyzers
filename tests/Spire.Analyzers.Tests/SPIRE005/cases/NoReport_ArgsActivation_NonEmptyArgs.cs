//@ should_pass
// Ensure that SPIRE005 is NOT triggered when args+activationAttributes overload is called with non-empty args.
public class NoReport_ArgsActivation_NonEmptyArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), new object[] { 42 }, (object[])null);
    }
}
