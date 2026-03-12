//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct), new object[0]) is a ternary branch.
public class Detect_Params_EmptyArgs_TernaryExpression
{
    public object Method(bool condition)
    {
        return condition
            ? Activator.CreateInstance(typeof(MustInitStruct), new object[0]) //~ ERROR
            : new object();
    }
}
