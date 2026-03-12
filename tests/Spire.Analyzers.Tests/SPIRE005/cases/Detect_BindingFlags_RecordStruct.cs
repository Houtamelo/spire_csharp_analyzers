//@ should_fail
// Ensure that SPIRE005 IS triggered when BindingFlags overload is called with a MustBeInit record struct and null args.
using System.Globalization;

public class Detect_BindingFlags_RecordStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitRecordStruct), BindingFlags.Default, null, null, null); //~ ERROR
    }
}
