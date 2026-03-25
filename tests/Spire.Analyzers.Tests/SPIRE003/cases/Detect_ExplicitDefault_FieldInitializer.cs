//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is used as an instance field initializer.
public class Detect_ExplicitDefault_FieldInitializer
{
    public EnforceInitializationStruct Field = default(EnforceInitializationStruct); //~ ERROR
}
