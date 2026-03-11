//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a record struct array with an explicit initializer.
public class NoReport_RecordStruct_WithInitializer
{
    public void Method()
    {
        var arr = new MustInitRecordStruct[] { new MustInitRecordStruct(1), new MustInitRecordStruct(2) };
    }
}
