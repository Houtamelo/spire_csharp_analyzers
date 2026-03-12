//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance args contains actual values.
public class NoReport_Params_NonEmptyArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), new object[] { 42 });
    }
}
