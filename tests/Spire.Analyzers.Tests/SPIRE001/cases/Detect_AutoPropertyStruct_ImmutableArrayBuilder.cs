//@ should_fail
// Ensure that SPIRE001 IS triggered when setting ImmutableArray.Builder.Count with a [MustBeInit] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_ImmutableArrayBuilder
{
    public void Method()
    {
        var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<MustInitStructWithAutoProperty>();
        builder.Count = 10; //~ ERROR
    }
}
