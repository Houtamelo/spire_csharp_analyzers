//@ should_pass
// Ensure that SPIRE016 is NOT triggered when (StatusNoZero)2 is returned — 2 matches Inactive.
public class NoReport_CastToEnum_ConstantValidValue_ReturnStatement
{
    public StatusNoZero Method()
    {
        return (StatusNoZero)2;
    }
}
