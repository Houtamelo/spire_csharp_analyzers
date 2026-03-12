//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor { }` appears in a lambda body.
public class Detect_ObjectInit_LambdaBody
{
    public void Method()
    {
        Func<MustInitNoCtor> f = () => new MustInitNoCtor { }; //~ ERROR
    }
}
