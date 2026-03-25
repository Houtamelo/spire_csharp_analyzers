//@ should_fail
// Ensure that SPIRE001 IS triggered when setting ImmutableArray.Builder.Count with a variable value.
public class Detect_ImmutableArrayBuilder_SetCount_Variable
{
    public void Method(int n)
    {
        var builder = ImmutableArray.CreateBuilder<EnforceInitializationStruct>();
        builder.Count = n; //~ ERROR
    }
}
