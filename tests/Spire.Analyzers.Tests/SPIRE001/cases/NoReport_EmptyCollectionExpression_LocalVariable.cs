//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using an empty collection expression.
public class NoReport_EmptyCollectionExpression_LocalVariable
{
    public void Method()
    {
        MustInitStruct[] arr = [];
    }
}
