//@ should_fail
// Ensure that SPIRE016 IS triggered when an arithmetic expression (x + 1) is cast to StatusNoZero.
public class Detect_CastToEnum_ArithmeticExpr_LocalVar
{
    public void Method(int x)
    {
        StatusNoZero val = (StatusNoZero)(x + 1); //~ ERROR
    }
}
