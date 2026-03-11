//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array in a field initializer.
public class Detect_1DConstantSize_FieldInitializer
{
    private MustInitStruct[] _arr = new MustInitStruct[5]; //~ ERROR
}
