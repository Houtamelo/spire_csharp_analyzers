//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct containing only a static auto-property.
[EnforceInitialization] //~ ERROR
public struct Detect_StaticPropertyOnly_Struct
{
    public static int Value { get; set; }
}
