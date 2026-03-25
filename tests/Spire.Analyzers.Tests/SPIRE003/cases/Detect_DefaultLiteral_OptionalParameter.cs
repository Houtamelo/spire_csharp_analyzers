//@ should_fail
// Ensure that SPIRE003 IS triggered when default is used as the default value of an optional parameter of type EnforceInitializationStruct.
public class Detect_DefaultLiteral_OptionalParameter
{
    public void Method(EnforceInitializationStruct s = default) //~ ERROR
    {
    }
}
