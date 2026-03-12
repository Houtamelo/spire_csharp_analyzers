//@ should_fail
// Ensure that SPIRE003 IS triggered when default is used in a branch of a ternary expression of type MustInitStruct.
public class Detect_DefaultLiteral_TernaryExpression
{
    public void Method(bool condition)
    {
        MustInitStruct c = condition ? new MustInitStruct(1) : default; //~ ERROR
    }
}
