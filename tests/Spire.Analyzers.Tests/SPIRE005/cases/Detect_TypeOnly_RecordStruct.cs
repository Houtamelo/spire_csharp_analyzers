//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitRecordStruct)) is used on a [MustBeInit] record struct.
public class Detect_TypeOnly_RecordStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitRecordStruct)); //~ ERROR
    }
}
