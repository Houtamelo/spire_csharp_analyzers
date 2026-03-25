//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` appears as the right operand of a null-coalescing expression.
public class Detect_NewT_NullCoalescing
{
    public EnforceInitializationNoCtor Method(EnforceInitializationNoCtor? maybeValue)
    {
        return maybeValue ?? new EnforceInitializationNoCtor(); //~ ERROR
    }
}
