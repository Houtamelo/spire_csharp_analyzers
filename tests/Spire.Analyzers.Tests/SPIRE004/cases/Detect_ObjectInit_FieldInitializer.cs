//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor { }` is used as a field initializer.
public class Detect_ObjectInit_FieldInitializer
{
    private MustInitNoCtor _field = new MustInitNoCtor { }; //~ ERROR
}
