//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a multi-dimensional array of a [MustBeInit] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_MultiDimensional
{
    public void Method()
    {
        var arr = new MustInitStructWithAutoProperty[3, 4]; //~ ERROR
    }
}
