//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using a non-empty collection expression.
public class NoReport_NonEmptyCollectionExpression_LocalVariable
{
    public void Method()
    {
        MustInitStruct[] arr = [new MustInitStruct(1), new MustInitStruct(2)];
    }
}
