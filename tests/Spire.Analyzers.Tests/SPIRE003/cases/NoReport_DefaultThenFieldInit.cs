//@ should_pass
// Ensure that SPIRE003 is NOT triggered when default(T) is followed by full field initialization.
public class NoReport_DefaultThenFieldInit
{
    public void Method()
    {
        var s = default(MustInitStruct);
        s.Value = 42;
        _ = s;
    }
}
