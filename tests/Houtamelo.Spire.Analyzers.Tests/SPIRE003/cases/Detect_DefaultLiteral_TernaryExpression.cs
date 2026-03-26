//@ should_fail
// Ensure that SPIRE003 IS triggered when default is used in a branch of a ternary expression of type EnforceInitializationStruct.
public class Detect_DefaultLiteral_TernaryExpression
{
    public void Method(bool condition)
    {
        EnforceInitializationStruct c = condition ? new EnforceInitializationStruct(1) : default; //~ ERROR
    }
}
