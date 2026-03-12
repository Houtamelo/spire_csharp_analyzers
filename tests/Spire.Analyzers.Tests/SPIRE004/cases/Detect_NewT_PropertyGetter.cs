//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() is returned from a property getter.
public class Detect_NewT_PropertyGetter
{
    public MustInitNoCtor Value
    {
        get
        {
            return new MustInitNoCtor(); //~ ERROR
        }
    }
}
