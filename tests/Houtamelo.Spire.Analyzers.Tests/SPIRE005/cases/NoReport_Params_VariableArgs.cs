//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance args is a variable.
public class NoReport_Params_VariableArgs
{
    public void Method()
    {
        object[] args = new object[] { 42 };
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct), args);
    }
}
