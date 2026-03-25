//@ should_fail
// Ensure that SPIRE001 IS triggered when setting ImmutableArray.Builder.Count.
public class Detect_ImmutableArrayBuilder_SetCount
{
    public void Method()
    {
        var builder = ImmutableArray.CreateBuilder<EnforceInitializationStruct>();
        builder.Count = 5; //~ ERROR
    }
}
