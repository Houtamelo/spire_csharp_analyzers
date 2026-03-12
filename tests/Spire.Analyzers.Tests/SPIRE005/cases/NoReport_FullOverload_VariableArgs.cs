//@ should_pass
// Ensure that SPIRE005 is NOT triggered when full overload args is a variable.
using System.Globalization;

public class NoReport_FullOverload_VariableArgs
{
    public void Method()
    {
        object[] args = new object[] { 42 };
        var x = Activator.CreateInstance(typeof(MustInitStruct), BindingFlags.Default, null, args, null, null);
    }
}
