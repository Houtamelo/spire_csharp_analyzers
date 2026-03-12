//@ should_fail
// Ensure that SPIRE005 IS triggered when BindingFlags overload args is new object[0].
using System.Globalization;

public class Detect_BindingFlags_ZeroLengthArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), BindingFlags.Default, null, new object[0], null); //~ ERROR
    }
}
