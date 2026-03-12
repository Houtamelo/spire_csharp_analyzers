//@ should_pass
// Ensure that SPIRE003 is NOT triggered when default literal is used with a reference type.
public class NoReport_DefaultLiteral_StringType
{
    public void Method()
    {
        string s = default;
    }
}
