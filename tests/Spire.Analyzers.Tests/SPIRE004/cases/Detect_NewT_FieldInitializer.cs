//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is used as an instance field initializer.
public class Detect_NewT_FieldInitializer
{
    private EnforceInitializationNoCtor _field = new EnforceInitializationNoCtor(); //~ ERROR
}
