//@ should_pass
// Ensure that SPIRE005 is NOT triggered when full overload is called with a plain struct type and null args.
using System.Globalization;

public class NoReport_FullOverload_PlainStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(PlainStruct), BindingFlags.Default, null, null, null, null);
    }
}
