//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an array of a [MustBeInit] record struct with no fields.
public class NoReport_EmptyRecordStruct_1DConstantSize
{
    public void Method()
    {
        var arr = new EmptyMustInitRecordStruct[5];
    }
}
