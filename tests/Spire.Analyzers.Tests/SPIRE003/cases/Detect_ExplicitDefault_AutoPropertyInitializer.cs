//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(EnforceInitializationStruct)` is used as an auto-property initializer.
public class Detect_ExplicitDefault_AutoPropertyInitializer
{
    public EnforceInitializationStruct Prop { get; set; } = default(EnforceInitializationStruct); //~ ERROR
}
