//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used on built-in value types like int and DateTime.
public class NoReport_BuiltinValueType_LocalVariable
{
    public void Method()
    {
        var i = new int();
        var dt = new DateTime();
        var g = new Guid();
        var ts = new TimeSpan();
    }
}
