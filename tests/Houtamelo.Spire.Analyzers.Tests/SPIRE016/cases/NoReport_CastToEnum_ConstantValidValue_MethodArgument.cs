//@ should_pass
// Ensure that SPIRE016 is NOT triggered when (StatusNoZero)3 is passed as an argument — 3 matches Pending.
public class NoReport_CastToEnum_ConstantValidValue_MethodArgument
{
    public void Consume(StatusNoZero value) { }

    public void Method()
    {
        Consume((StatusNoZero)3);
    }
}
