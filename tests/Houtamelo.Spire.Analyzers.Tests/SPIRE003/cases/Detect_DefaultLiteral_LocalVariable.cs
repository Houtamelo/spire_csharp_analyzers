//@ should_fail
// Ensure that SPIRE003 IS triggered when default literal is used in a local variable declaration of EnforceInitializationStruct.
public class Detect_DefaultLiteral_LocalVariable
{
    public void Method()
    {
        EnforceInitializationStruct c = default; //~ ERROR
    }
}
