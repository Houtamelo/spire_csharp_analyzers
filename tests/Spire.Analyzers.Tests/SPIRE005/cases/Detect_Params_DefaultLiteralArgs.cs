//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance args is (object[])default.
public class Detect_Params_DefaultLiteralArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), (object[])default); //~ ERROR
    }
}
