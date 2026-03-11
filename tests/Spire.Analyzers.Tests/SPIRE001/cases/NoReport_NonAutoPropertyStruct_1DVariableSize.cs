//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a variable-size array of a [MustBeInit] struct whose only property is non-auto (no backing field).
public class NoReport_NonAutoPropertyStruct_1DVariableSize
{
    public void Method(int n)
    {
        var arr = new MustInitStructWithNonAutoProperty[n];
    }
}
