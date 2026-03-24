//@ should_fail
// Ensure that SPIRE016 IS triggered when (StatusNoZero)5 appears in a ternary expression.
public class Detect_CastToEnum_ConstantInvalidValue_TernaryExpr
{
    public StatusNoZero Method(bool condition)
    {
        return condition ? StatusNoZero.Active : (StatusNoZero)5; //~ ERROR
    }
}
