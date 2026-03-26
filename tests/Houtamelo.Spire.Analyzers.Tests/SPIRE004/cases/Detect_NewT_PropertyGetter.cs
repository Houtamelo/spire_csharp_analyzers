//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() is returned from a property getter.
public class Detect_NewT_PropertyGetter
{
    public EnforceInitializationNoCtor Value
    {
        get
        {
            return new EnforceInitializationNoCtor(); //~ ERROR
        }
    }
}
