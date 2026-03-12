//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(MustInitStruct)` is the body of an expression-bodied property getter.
public class Detect_ExplicitDefault_ExpressionBodiedProperty
{
    public MustInitStruct Prop => default(MustInitStruct); //~ ERROR
}
