//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitAutoPropsNoCtor()` — struct with auto-properties but no parameterless ctor.
public class Detect_AutoPropsNoCtor_LocalVariable
{
    public void Method()
    {
        var x = new MustInitAutoPropsNoCtor(); //~ ERROR
    }
}
