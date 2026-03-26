//@ should_pass
// Ensure that SPIRE016 is NOT triggered when (FlagsWithZero)0 is cast — 0 matches None.
public class NoReport_CastToEnum_ValidValue_FlagsWithZero_Zero
{
    public void Method()
    {
        FlagsWithZero x = (FlagsWithZero)0;
    }
}
