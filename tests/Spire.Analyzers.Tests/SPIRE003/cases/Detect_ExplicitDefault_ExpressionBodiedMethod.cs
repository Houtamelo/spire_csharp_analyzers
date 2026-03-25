//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is the body of an expression-bodied method.
public class Detect_ExplicitDefault_ExpressionBodiedMethod
{
    public EnforceInitializationStruct Method() => default(EnforceInitializationStruct); //~ ERROR
}
