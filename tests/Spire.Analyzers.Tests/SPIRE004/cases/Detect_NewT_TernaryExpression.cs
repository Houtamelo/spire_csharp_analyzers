//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor()` appears as a branch of a ternary expression.
public class Detect_NewT_TernaryExpression
{
    public MustInitNoCtor Method(bool condition)
    {
        return condition
            ? new MustInitNoCtor(1, "set")
            : new MustInitNoCtor(); //~ ERROR
    }
}
