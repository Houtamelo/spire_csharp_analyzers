//@ should_pass
// Ensure that SPIRE001 is NOT triggered when setting ImmutableArray.Builder.Count on a plain struct.
public class NoReport_ImmutableArrayBuilder_PlainStruct
{
    public void Method()
    {
        var builder = ImmutableArray.CreateBuilder<PlainStruct>();
        builder.Count = 5;
    }
}
