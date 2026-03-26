//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the target is an enum type.
public enum MyEnum { A, B }

public class NoReport_EnumType
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(MyEnum));
    }
}
