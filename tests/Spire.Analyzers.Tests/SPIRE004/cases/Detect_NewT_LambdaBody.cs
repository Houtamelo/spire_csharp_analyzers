//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor()` appears in the body of a lambda.
public class Detect_NewT_LambdaBody
{
    public void Method()
    {
        Func<MustInitNoCtor> factory = () => new MustInitNoCtor(); //~ ERROR
    }
}
