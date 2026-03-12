//@ should_fail
// Ensure that SPIRE003 IS triggered when default literal is used as an instance field initializer for MustInitStruct.
public class Detect_DefaultLiteral_FieldInitializer
{
    public MustInitStruct Field = default; //~ ERROR
}
