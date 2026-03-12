//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() is used as an expression-bodied property.
public class Detect_NewT_ExpressionBodiedProperty
{
    public MustInitNoCtor Value => new MustInitNoCtor(); //~ ERROR
}
