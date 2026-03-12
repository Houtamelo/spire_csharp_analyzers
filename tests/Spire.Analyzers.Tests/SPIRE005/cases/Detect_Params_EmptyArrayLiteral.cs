//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance args is new object[] { }.
public class Detect_Params_EmptyArrayLiteral
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), new object[] { }); //~ ERROR
    }
}
