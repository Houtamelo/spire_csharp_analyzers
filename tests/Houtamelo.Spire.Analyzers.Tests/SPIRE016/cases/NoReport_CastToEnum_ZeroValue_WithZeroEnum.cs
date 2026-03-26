//@ should_pass
// Ensure that SPIRE016 is NOT triggered when (StatusWithZero)0 is cast — 0 matches None.
public class NoReport_CastToEnum_ZeroValue_WithZeroEnum
{
    public void Method()
    {
        StatusWithZero x = (StatusWithZero)0;
    }
}
