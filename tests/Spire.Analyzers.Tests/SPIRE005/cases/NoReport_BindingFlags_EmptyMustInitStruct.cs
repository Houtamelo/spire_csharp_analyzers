//@ should_pass
// Ensure that SPIRE005 is NOT triggered when BindingFlags overload is called with EmptyEnforceInitializationStruct and null args.
using System.Globalization;

public class NoReport_BindingFlags_EmptyEnforceInitializationStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyEnforceInitializationStruct), BindingFlags.Default, null, null, null);
    }
}
