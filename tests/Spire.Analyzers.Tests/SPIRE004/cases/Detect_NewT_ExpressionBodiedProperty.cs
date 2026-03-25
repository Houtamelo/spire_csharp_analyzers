//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() is used as an expression-bodied property.
public class Detect_NewT_ExpressionBodiedProperty
{
    public EnforceInitializationNoCtor Value => new EnforceInitializationNoCtor(); //~ ERROR
}
