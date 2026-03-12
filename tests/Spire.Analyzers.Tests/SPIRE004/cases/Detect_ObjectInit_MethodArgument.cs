//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor { }` is passed as a method argument.
public class Detect_ObjectInit_MethodArgument
{
    private static void Consume(MustInitNoCtor value) { }

    public void Method()
    {
        Consume(new MustInitNoCtor { }); //~ ERROR
    }
}
