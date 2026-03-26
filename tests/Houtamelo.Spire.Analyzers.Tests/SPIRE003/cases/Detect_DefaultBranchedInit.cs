//@ should_fail
// Ensure that SPIRE003 IS triggered when one branch leaves the variable as default.
public class Detect_DefaultBranchedInit
{
    public void Method(bool cond)
    {
        EnforceInitializationStruct s;
        if (cond)
            s = new EnforceInitializationStruct(1);
        else
            s = default(EnforceInitializationStruct); //~ ERROR
        _ = s;
    }
}
