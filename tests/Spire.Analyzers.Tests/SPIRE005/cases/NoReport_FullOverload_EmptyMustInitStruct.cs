//@ should_pass
// Ensure that SPIRE005 is NOT triggered when full overload is called with EmptyMustInitStruct and null args.
using System.Globalization;

public class NoReport_FullOverload_EmptyMustInitStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyMustInitStruct), BindingFlags.Default, null, null, null, null);
    }
}
