//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) appears inside a lambda body.
public class Detect_ArrayClear1_InLambda
{
    public void Method()
    {
        var arr = new MustInitStruct[5];
        Action a = () => Array.Clear(arr); //~ ERROR
    }
}
