//@ should_fail
// Ensure that SPIRE001 IS triggered when creating an array of a [MustBeInit] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_1DConstantSize
{
    public void Method()
    {
        var arr = new MustInitStructWithAutoProperty[5]; //~ ERROR
    }
}
