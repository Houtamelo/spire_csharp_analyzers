//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array in a null-coalescing expression.
public class Detect_1DConstantSize_NullCoalescing
{
    public void Method(MustInitStruct[]? existing)
    {
        var arr = existing ?? new MustInitStruct[5]; //~ ERROR
    }
}
