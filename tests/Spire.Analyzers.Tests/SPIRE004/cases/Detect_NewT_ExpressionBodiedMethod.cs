//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is the body of an expression-bodied method.
public class Detect_NewT_ExpressionBodiedMethod
{
    public EnforceInitializationNoCtor Method() => new EnforceInitializationNoCtor(); //~ ERROR
}
