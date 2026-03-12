//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor()` is passed as an argument to a constructor.
public class Detect_NewT_ConstructorArgument
{
    private class Wrapper
    {
        public Wrapper(MustInitNoCtor value) { }
    }

    public void Method()
    {
        var w = new Wrapper(new MustInitNoCtor()); //~ ERROR
    }
}
