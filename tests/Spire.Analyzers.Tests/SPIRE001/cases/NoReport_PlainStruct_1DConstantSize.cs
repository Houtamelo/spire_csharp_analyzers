//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an array of a plain struct (no [MustBeInit]).
public class NoReport_PlainStruct_1DConstantSize
{
    public void Method()
    {
        var arr = new PlainStruct[5];
    }
}
