//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor { }` is used as a field initializer.
public class Detect_ObjectInit_FieldInitializer
{
    private EnforceInitializationNoCtor _field = new EnforceInitializationNoCtor { }; //~ ERROR
}
