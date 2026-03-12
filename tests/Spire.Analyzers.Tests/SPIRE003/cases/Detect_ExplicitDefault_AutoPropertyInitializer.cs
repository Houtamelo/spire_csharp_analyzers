//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(MustInitStruct)` is used as an auto-property initializer.
public class Detect_ExplicitDefault_AutoPropertyInitializer
{
    public MustInitStruct Prop { get; set; } = default(MustInitStruct); //~ ERROR
}
