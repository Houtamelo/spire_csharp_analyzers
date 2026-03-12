//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(MustInitStruct)` is used as a static field initializer.
public class Detect_ExplicitDefault_StaticFieldInitializer
{
    public static MustInitStruct Field = default(MustInitStruct); //~ ERROR
}
