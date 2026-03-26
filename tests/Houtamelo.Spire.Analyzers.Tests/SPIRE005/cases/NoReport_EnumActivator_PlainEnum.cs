//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance<PlainEnum>() is used on an enum not marked with [EnforceInitialization].
public class NoReport_EnumActivator_PlainEnum
{
    public void Method()
    {
        var e = Activator.CreateInstance<PlainEnum>();
    }
}
