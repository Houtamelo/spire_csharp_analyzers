//@ should_pass
// Ensure that SPIRE001 is NOT triggered when setting ImmutableArray.Builder.Count to zero.
public class NoReport_ImmutableArrayBuilder_SetCount_Zero
{
    public void Method()
    {
        var builder = ImmutableArray.CreateBuilder<MustInitStruct>();
        builder.Count = 0;
    }
}
