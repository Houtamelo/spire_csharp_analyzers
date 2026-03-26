//@ should_pass
// Ensure that SPIRE007 is NOT triggered when Unsafe.SkipInit is called on a plain enum (no [EnforceInitialization]).
public class NoReport_EnumSkipInit_PlainEnum
{
    void M()
    {
        Unsafe.SkipInit(out PlainEnum e);
    }
}
