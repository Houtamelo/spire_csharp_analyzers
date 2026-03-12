//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor()` is used as a static field initializer.
public class Detect_NewT_StaticFieldInitializer
{
    private static MustInitNoCtor _field = new MustInitNoCtor(); //~ ERROR
}
