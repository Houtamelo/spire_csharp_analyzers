//@ should_fail
// Ensure that SPIRE003 IS triggered when default literal is used as a static field initializer for EnforceInitializationStruct.
public class Detect_DefaultLiteral_StaticFieldInitializer
{
    public static EnforceInitializationStruct Field = default; //~ ERROR
}
