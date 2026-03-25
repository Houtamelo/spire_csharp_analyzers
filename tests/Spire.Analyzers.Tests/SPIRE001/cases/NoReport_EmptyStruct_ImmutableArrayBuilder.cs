//@ should_pass
// Ensure that SPIRE001 is NOT triggered when setting ImmutableArray.Builder.Count with a [EnforceInitialization] struct with no fields.
public class NoReport_EmptyStruct_ImmutableArrayBuilder
{
    public void Method()
    {
        var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<EmptyEnforceInitializationStruct>();
        builder.Count = 10;
    }
}
