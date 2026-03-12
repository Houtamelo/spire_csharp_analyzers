//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() appears inside a nested class.
public class Detect_NewT_NestedInClass
{
    public class Inner
    {
        public MustInitNoCtor Method()
        {
            return new MustInitNoCtor(); //~ ERROR
        }
    }
}
