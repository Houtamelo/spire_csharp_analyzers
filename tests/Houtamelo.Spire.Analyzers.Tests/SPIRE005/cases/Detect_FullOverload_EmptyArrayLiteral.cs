//@ should_fail
// Ensure that SPIRE005 IS triggered when full overload args is an empty array literal.
using System.Globalization;

public class Detect_FullOverload_EmptyArrayLiteral
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct), BindingFlags.Default, null, new object[] { }, null, null); //~ ERROR
    }
}
