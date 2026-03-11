//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a variable-size array of a [MustBeInit] struct with no fields.
public class NoReport_EmptyStruct_1DVariableSize
{
    public void Method(int n)
    {
        var arr = new EmptyMustInitStruct[n];
    }
}
