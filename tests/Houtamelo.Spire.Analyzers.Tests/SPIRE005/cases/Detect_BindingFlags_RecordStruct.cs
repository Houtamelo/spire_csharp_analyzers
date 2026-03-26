//@ should_fail
// Ensure that SPIRE005 IS triggered when BindingFlags overload is called with a EnforceInitialization record struct and null args.
using System.Globalization;

public class Detect_BindingFlags_RecordStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationRecordStruct), BindingFlags.Default, null, null, null); //~ ERROR
    }
}
