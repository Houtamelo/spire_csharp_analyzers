//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an array of a [MustBeInit] struct whose only property is non-auto (no backing field).
public class NoReport_NonAutoPropertyStruct_1DConstantSize
{
    public void Method()
    {
        var arr = new MustInitStructWithNonAutoProperty[5];
    }
}
