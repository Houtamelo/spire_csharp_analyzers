//@ should_fail
// Ensure that SPIRE003 IS triggered when one branch leaves the variable as default.
public class Detect_DefaultBranchedInit
{
    public void Method(bool cond)
    {
        MustInitStruct s;
        if (cond)
            s = new MustInitStruct(1);
        else
            s = default(MustInitStruct); //~ ERROR
        _ = s;
    }
}
