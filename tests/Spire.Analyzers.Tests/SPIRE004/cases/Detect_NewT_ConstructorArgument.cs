//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is passed as an argument to a constructor.
public class Detect_NewT_ConstructorArgument
{
    private class Wrapper
    {
        public Wrapper(EnforceInitializationNoCtor value) { }
    }

    public void Method()
    {
        var w = new Wrapper(new EnforceInitializationNoCtor()); //~ ERROR
    }
}
