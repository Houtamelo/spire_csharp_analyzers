//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance args is default(object[]).
public class Detect_Params_DefaultArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), default(object[])); //~ ERROR
    }
}
