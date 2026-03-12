//@ should_pass
// Ensure that SPIRE005 is NOT triggered when BindingFlags overload args has values.
using System.Globalization;

public class NoReport_BindingFlags_NonEmptyArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), BindingFlags.Default, null, new object[] { 42 }, null);
    }
}
