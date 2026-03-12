//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitStruct>() is used in a ternary branch.
public class Detect_GenericOverload_TernaryExpression
{
    public MustInitStruct Method(bool condition)
    {
        return condition ? new MustInitStruct(1) : Activator.CreateInstance<MustInitStruct>(); //~ ERROR
    }
}
