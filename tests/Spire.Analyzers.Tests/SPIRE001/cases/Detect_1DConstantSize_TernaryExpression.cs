//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array in a ternary expression.
public class Detect_1DConstantSize_TernaryExpression
{
    public MustInitStruct[] Method(bool flag)
    {
        return flag ? new MustInitStruct[5] : new MustInitStruct[] { new MustInitStruct(1) }; //~ ERROR
    }
}
