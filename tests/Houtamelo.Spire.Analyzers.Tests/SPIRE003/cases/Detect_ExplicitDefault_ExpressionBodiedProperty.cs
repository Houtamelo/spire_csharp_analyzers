//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is the body of an expression-bodied property getter.
public class Detect_ExplicitDefault_ExpressionBodiedProperty
{
    public EnforceInitializationStruct Prop => default(EnforceInitializationStruct); //~ ERROR
}
