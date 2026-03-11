//@ should_fail
// Ensure that SPIRE001 IS triggered when calling Array.CreateInstance in a ternary expression.
public class Detect_ArrayCreateInstance_TernaryExpression
{
    public Array Method(bool flag)
    {
        return flag
            ? Array.CreateInstance(typeof(MustInitStruct), 5) //~ ERROR
            : Array.CreateInstance(typeof(MustInitStruct), 0);
    }
}
