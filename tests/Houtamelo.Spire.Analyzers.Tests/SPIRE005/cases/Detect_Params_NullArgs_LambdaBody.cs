//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationStruct), (object[])null) is inside a lambda.
public class Detect_Params_NullArgs_LambdaBody
{
    public void Method()
    {
        Action action = () =>
        {
            var result = Activator.CreateInstance(typeof(EnforceInitializationStruct), (object[]?)null); //~ ERROR
        };
    }
}
