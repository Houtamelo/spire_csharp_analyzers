//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(MustInitStruct)` is the body of an expression-bodied method.
public class Detect_ExplicitDefault_ExpressionBodiedMethod
{
    public MustInitStruct Method() => default(MustInitStruct); //~ ERROR
}
