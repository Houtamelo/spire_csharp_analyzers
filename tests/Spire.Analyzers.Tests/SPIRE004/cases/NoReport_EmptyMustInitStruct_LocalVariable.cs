//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used on a [MustBeInit] struct with no fields.
public class NoReport_EmptyMustInitStruct_LocalVariable
{
    public void Method()
    {
        var x = new EmptyMustInitStruct();
    }
}
