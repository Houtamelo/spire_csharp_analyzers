//@ should_pass
// Ensure that SPIRE001 is NOT triggered when setting ImmutableArray.Builder.Count with a [EnforceInitialization] struct whose only property is non-auto (no backing field).
public class NoReport_NonAutoPropertyStruct_ImmutableArrayBuilder
{
    public void Method()
    {
        var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<EnforceInitializationStructWithNonAutoProperty>();
        builder.Count = 10;
    }
}
