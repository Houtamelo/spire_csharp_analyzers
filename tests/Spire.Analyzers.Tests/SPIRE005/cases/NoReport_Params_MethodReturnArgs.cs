//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance args is a method return value.
public class NoReport_Params_MethodReturnArgs
{
    private static object[] GetArgs() => new object[] { 42 };

    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), GetArgs());
    }
}
