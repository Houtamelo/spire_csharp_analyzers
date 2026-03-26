//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` appears in the false branch of a ternary expression.
public class Detect_ExplicitDefault_TernaryExpression
{
    public EnforceInitializationStruct Method(bool condition, EnforceInitializationStruct s)
    {
        return condition ? s : default(EnforceInitializationStruct); //~ ERROR
    }
}
