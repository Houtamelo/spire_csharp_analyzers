//@ should_fail
// Ensure that SPIRE001 IS triggered when creating a 1D constant-size array in a field initializer.
public class Detect_1DConstantSize_FieldInitializer
{
    private EnforceInitializationStruct[] _arr = new EnforceInitializationStruct[5]; //~ ERROR
}
