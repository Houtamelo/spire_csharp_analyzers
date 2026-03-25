//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is used as an auto-property initializer.
public class Detect_NewT_AutoPropertyInitializer
{
    public EnforceInitializationNoCtor Prop { get; set; } = new EnforceInitializationNoCtor(); //~ ERROR
}
