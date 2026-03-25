//@ should_pass
// Ensure that SPIRE003 is NOT triggered when default literal is used with a struct not marked [EnforceInitialization].
public class NoReport_DefaultLiteral_PlainStruct
{
    public void Method()
    {
        PlainStruct s = default;
    }
}
