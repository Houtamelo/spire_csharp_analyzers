//@ should_fail
// Ensure that SPIRE003 IS triggered when default literal is used as an auto-property initializer for EnforceInitializationStruct.
public class Detect_DefaultLiteral_AutoPropertyInitializer
{
    public EnforceInitializationStruct Property { get; set; } = default; //~ ERROR
}
