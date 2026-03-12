//@ should_pass
// Ensure that SPIRE005 is NOT triggered when BindingFlags overload is called with EmptyMustInitStruct and null args.
using System.Globalization;

public class NoReport_BindingFlags_EmptyMustInitStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyMustInitStruct), BindingFlags.Default, null, null, null);
    }
}
