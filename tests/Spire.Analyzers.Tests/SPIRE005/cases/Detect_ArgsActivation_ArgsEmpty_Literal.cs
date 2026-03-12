//@ should_fail
// Ensure that SPIRE005 IS triggered when args+activationAttributes overload is called with empty array literal args.
public class Detect_ArgsActivation_ArgsEmpty_Literal
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), new object[] { }, (object[])null); //~ ERROR
    }
}
