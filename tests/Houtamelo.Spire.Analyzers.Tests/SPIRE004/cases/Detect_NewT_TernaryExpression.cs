//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears as a branch of a ternary expression.
public class Detect_NewT_TernaryExpression
{
    public EnforceInitializationNoCtor Method(bool condition)
    {
        return condition
            ? new EnforceInitializationNoCtor(1, "set")
            : new EnforceInitializationNoCtor(); //~ ERROR
    }
}
