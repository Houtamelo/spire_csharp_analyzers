//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor()` is the body of an expression-bodied method.
public class Detect_NewT_ExpressionBodiedMethod
{
    public MustInitNoCtor Method() => new MustInitNoCtor(); //~ ERROR
}
