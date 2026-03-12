//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance(typeof(EmptyMustInitStruct)) is used on a fieldless [MustBeInit] struct.
public class NoReport_TypeOnly_EmptyMustInitStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyMustInitStruct));
    }
}
