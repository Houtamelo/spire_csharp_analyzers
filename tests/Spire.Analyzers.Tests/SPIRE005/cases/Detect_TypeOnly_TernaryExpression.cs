//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationStruct)) is used in a ternary branch.
public class Detect_TypeOnly_TernaryExpression
{
    public object Method(bool condition)
    {
        return condition ? new EnforceInitializationStruct(1) : Activator.CreateInstance(typeof(EnforceInitializationStruct)); //~ ERROR
    }
}
