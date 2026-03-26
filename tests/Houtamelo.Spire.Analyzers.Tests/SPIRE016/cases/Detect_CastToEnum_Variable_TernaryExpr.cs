//@ should_fail
// Ensure that SPIRE016 IS triggered when a variable cast to StatusNoZero appears in a ternary expression.
public class Detect_CastToEnum_Variable_TernaryExpr
{
    public StatusNoZero Method(bool condition, int someVar)
    {
        return condition ? StatusNoZero.Active : (StatusNoZero)someVar; //~ ERROR
    }
}
