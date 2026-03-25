//@ should_fail
// Ensure that SPIRE003 IS triggered when only some fields are initialized after default.
public class Detect_DefaultPartialFieldInit
{
    public void Method()
    {
        var s = default(UnionLikeStruct); //~ ERROR
        s.Kind = 1;
        // s.Value not initialized
        _ = s;
    }
}
