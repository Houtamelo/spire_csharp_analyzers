//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance args is new object[0].
public class Detect_Params_ZeroLengthArray
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), new object[0]); //~ ERROR
    }
}
