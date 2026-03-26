//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using a non-empty collection expression for a record struct array.
public class NoReport_RecordStruct_CollectionExpression
{
    public void Method()
    {
        EnforceInitializationRecordStruct[] arr = [new EnforceInitializationRecordStruct(1)];
    }
}
