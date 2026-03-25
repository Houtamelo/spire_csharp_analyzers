//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct whose only member is a non-auto get/set property with block accessors.
[EnforceInitialization] //~ ERROR
public struct Detect_NonAutoProperty_GetSetBlock_Struct
{
    public int Value { get { return 42; } set { } }
}
