//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance<MustInitEnumNoZero>() is called and no zero-valued member exists.
public class Detect_EnumActivator_Generic_NoZero
{
    public void Method()
    {
        var e = Activator.CreateInstance<MustInitEnumNoZero>(); //~ ERROR
    }
}
