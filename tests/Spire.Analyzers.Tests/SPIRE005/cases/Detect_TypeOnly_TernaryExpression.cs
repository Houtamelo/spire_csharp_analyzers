//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct)) is used in a ternary branch.
public class Detect_TypeOnly_TernaryExpression
{
    public object Method(bool condition)
    {
        return condition ? new MustInitStruct(1) : Activator.CreateInstance(typeof(MustInitStruct)); //~ ERROR
    }
}
