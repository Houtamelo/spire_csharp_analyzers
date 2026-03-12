//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor()` is used as an instance field initializer.
public class Detect_NewT_FieldInitializer
{
    private MustInitNoCtor _field = new MustInitNoCtor(); //~ ERROR
}
