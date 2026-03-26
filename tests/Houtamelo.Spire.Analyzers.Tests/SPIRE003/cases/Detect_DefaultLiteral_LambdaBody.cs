//@ should_fail
// Ensure that SPIRE003 IS triggered when default is returned from a lambda returning EnforceInitializationStruct.
public class Detect_DefaultLiteral_LambdaBody
{
    public void Method()
    {
        System.Func<EnforceInitializationStruct> f = () => default; //~ ERROR
    }
}
