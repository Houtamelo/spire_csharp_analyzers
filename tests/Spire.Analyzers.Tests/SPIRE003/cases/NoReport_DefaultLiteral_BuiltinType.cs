//@ should_pass
// Ensure that SPIRE003 is NOT triggered when default literal is used with a built-in type.
public class NoReport_DefaultLiteral_BuiltinType
{
    public void Method()
    {
        int x = default;
    }
}
