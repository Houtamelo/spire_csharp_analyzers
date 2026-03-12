//@ should_fail
// Ensure that SPIRE003 IS triggered when `default(MustInitStruct)` is used as an instance field initializer.
public class Detect_ExplicitDefault_FieldInitializer
{
    public MustInitStruct Field = default(MustInitStruct); //~ ERROR
}
