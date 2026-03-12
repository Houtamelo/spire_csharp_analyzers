//@ should_fail
// Ensure that SPIRE003 IS triggered when default is used as an expression-bodied property body of type MustInitStruct.
public class Detect_DefaultLiteral_ExpressionBodiedProperty
{
    public MustInitStruct Property => default; //~ ERROR
}
