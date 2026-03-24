//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitEnumNoZero)) is called and no zero-valued member exists.
public class Detect_EnumActivator_TypeOf_NoZero
{
    public void Method()
    {
        var e = Activator.CreateInstance(typeof(MustInitEnumNoZero)); //~ ERROR
    }
}
