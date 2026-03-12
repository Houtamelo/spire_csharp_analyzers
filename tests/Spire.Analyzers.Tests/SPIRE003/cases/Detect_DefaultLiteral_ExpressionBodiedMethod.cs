//@ should_fail
// Ensure that SPIRE003 IS triggered when default is used as an expression-bodied method body returning MustInitStruct.
public class Detect_DefaultLiteral_ExpressionBodiedMethod
{
    public MustInitStruct Method() => default; //~ ERROR
}
