//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array in a ternary expression.
public class Detect_1DConstantSize_TernaryExpression
{
    public EnforceInitializationStruct[] Method(bool flag)
    {
        return flag ? new EnforceInitializationStruct[5] : new EnforceInitializationStruct[] { new EnforceInitializationStruct(1) }; //~ ERROR
    }
}
