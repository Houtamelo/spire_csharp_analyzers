//@ should_fail
// Ensure that SPIRE001 IS triggered when setting ImmutableArray.Builder.Count with a [EnforceInitialization] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_ImmutableArrayBuilder
{
    public void Method()
    {
        var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<EnforceInitializationStructWithAutoProperty>();
        builder.Count = 10; //~ ERROR
    }
}
