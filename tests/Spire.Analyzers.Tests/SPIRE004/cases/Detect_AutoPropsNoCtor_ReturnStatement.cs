//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitAutoPropsNoCtor()` is returned from a method.
public class Detect_AutoPropsNoCtor_ReturnStatement
{
    public MustInitAutoPropsNoCtor Method()
    {
        return new MustInitAutoPropsNoCtor(); //~ ERROR
    }
}
