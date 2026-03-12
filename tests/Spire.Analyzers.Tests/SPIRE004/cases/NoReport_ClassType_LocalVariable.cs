//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used on a class type, even without [MustBeInit].
public class NoReport_ClassType_LocalVariable
{
    public void Method()
    {
        var x = new SomeClass();
    }
}
