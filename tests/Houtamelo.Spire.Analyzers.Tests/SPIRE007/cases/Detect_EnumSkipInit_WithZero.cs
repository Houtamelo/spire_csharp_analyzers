//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit is called on a [EnforceInitialization] enum with a zero-valued member — garbage data, not zero-init.
public class Detect_EnumSkipInit_WithZero
{
    void M()
    {
        Unsafe.SkipInit(out EnforceInitializationEnumWithZero e); //~ ERROR
    }
}
