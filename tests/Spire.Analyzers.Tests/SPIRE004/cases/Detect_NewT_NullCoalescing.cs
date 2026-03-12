//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor()` appears as the right operand of a null-coalescing expression.
public class Detect_NewT_NullCoalescing
{
    public MustInitNoCtor Method(MustInitNoCtor? maybeValue)
    {
        return maybeValue ?? new MustInitNoCtor(); //~ ERROR
    }
}
