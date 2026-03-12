//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance args is (object[])null.
public class Detect_Params_NullArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), (object[])null); //~ ERROR
    }
}
