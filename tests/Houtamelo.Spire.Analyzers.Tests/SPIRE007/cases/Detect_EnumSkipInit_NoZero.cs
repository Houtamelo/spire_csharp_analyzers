//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit is called on a [EnforceInitialization] enum with no zero-valued member.
public class Detect_EnumSkipInit_NoZero
{
    void M()
    {
        Unsafe.SkipInit(out EnforceInitializationEnumNoZero e); //~ ERROR
    }
}
