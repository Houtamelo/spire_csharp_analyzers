//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationRecordStructWithFields()` — record struct with regular fields, no parameterless ctor.
public class Detect_RecordStructWithFields_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationRecordStructWithFields(); //~ ERROR
    }
}
