//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is used as a static field initializer.
public class Detect_ExplicitDefault_StaticFieldInitializer
{
    public static EnforceInitializationStruct Field = default(EnforceInitializationStruct); //~ ERROR
}
