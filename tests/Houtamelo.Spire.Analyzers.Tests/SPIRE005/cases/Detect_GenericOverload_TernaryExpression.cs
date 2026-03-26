//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<EnforceInitializationStruct>() is used in a ternary branch.
public class Detect_GenericOverload_TernaryExpression
{
    public EnforceInitializationStruct Method(bool condition)
    {
        return condition ? new EnforceInitializationStruct(1) : Activator.CreateInstance<EnforceInitializationStruct>(); //~ ERROR
    }
}
