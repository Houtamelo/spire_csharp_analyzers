//@ should_fail
// Ensure that SPIRE003 IS triggered when default literal is used as an auto-property initializer for MustInitStruct.
public class Detect_DefaultLiteral_AutoPropertyInitializer
{
    public MustInitStruct Property { get; set; } = default; //~ ERROR
}
