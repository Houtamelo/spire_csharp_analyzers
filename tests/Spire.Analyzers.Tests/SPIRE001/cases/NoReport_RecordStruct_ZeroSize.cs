//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a zero-length array of a record struct.
public class NoReport_RecordStruct_ZeroSize
{
    public void Method()
    {
        var arr = new MustInitRecordStruct[0];
    }
}
