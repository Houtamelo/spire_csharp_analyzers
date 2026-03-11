//@ should_fail
// Ensure that SPIRE001 IS triggered when calling ArrayPool.Rent on a custom pool instance.
public class Detect_ArrayPoolCustom_Rent
{
    public void Method(ArrayPool<MustInitStruct> pool)
    {
        var arr = pool.Rent(5); //~ ERROR
    }
}
