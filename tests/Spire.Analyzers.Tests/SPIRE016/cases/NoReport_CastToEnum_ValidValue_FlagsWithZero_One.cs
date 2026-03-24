//@ should_pass
// Ensure that SPIRE016 is NOT triggered when (FlagsWithZero)1 is cast — 1 matches Read.
public class NoReport_CastToEnum_ValidValue_FlagsWithZero_One
{
    public void Method()
    {
        FlagsWithZero x = (FlagsWithZero)1;
    }
}
