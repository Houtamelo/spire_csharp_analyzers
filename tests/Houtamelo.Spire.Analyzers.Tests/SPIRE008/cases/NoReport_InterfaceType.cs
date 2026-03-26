//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the target is an interface type.
public interface IMyInterface { }

public class NoReport_InterfaceType
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(IMyInterface));
    }
}
