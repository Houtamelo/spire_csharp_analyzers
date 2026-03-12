//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is returned for a [MustBeInit] struct with no fields.
public class NoReport_EmptyMustInitStruct_ReturnStatement
{
    public EmptyMustInitStruct Method()
    {
        return new EmptyMustInitStruct();
    }
}
