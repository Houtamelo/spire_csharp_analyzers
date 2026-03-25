//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() appears inside a nested class.
public class Detect_NewT_NestedInClass
{
    public class Inner
    {
        public EnforceInitializationNoCtor Method()
        {
            return new EnforceInitializationNoCtor(); //~ ERROR
        }
    }
}
