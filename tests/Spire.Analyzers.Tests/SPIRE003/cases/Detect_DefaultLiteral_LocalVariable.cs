//@ should_fail
// Ensure that SPIRE003 IS triggered when default literal is used in a local variable declaration of MustInitStruct.
public class Detect_DefaultLiteral_LocalVariable
{
    public void Method()
    {
        MustInitStruct c = default; //~ ERROR
    }
}
