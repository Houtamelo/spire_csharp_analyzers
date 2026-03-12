//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor()` is used as an auto-property initializer.
public class Detect_NewT_AutoPropertyInitializer
{
    public MustInitNoCtor Prop { get; set; } = new MustInitNoCtor(); //~ ERROR
}
