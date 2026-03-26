//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is used as a static field initializer.
public class Detect_NewT_StaticFieldInitializer
{
    private static EnforceInitializationNoCtor _field = new EnforceInitializationNoCtor(); //~ ERROR
}
