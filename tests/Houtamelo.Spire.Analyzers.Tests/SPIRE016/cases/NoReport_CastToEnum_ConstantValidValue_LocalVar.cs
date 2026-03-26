//@ should_pass
// Ensure that SPIRE016 is NOT triggered when (StatusNoZero)1 is assigned — 1 matches Active.
public class NoReport_CastToEnum_ConstantValidValue_LocalVar
{
    public void Method()
    {
        StatusNoZero x = (StatusNoZero)1;
    }
}
