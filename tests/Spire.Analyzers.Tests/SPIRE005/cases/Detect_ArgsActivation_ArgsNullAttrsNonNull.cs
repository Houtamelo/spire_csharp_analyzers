//@ should_fail
// Ensure that SPIRE005 IS triggered when args+activationAttributes overload is called with null args and non-null activationAttributes.
public class Detect_ArgsActivation_ArgsNullAttrsNonNull
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), (object[])null, new object[] { "attr" }); //~ ERROR
    }
}
