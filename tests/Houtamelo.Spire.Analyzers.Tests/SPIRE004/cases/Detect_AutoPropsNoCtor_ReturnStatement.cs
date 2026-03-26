//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationAutoPropsNoCtor()` is returned from a method.
public class Detect_AutoPropsNoCtor_ReturnStatement
{
    public EnforceInitializationAutoPropsNoCtor Method()
    {
        return new EnforceInitializationAutoPropsNoCtor(); //~ ERROR
    }
}
