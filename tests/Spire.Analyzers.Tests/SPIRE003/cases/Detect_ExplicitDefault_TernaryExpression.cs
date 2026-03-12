//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(MustInitStruct)` appears in the false branch of a ternary expression.
public class Detect_ExplicitDefault_TernaryExpression
{
    public MustInitStruct Method(bool condition, MustInitStruct s)
    {
        return condition ? s : default(MustInitStruct); //~ ERROR
    }
}
