//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance<EnforceInitializationEnumWithZero>() is used and a zero-valued member (None=0) exists.
public class NoReport_EnumActivator_WithZeroMember
{
    public void Method()
    {
        var e = Activator.CreateInstance<EnforceInitializationEnumWithZero>();
    }
}
