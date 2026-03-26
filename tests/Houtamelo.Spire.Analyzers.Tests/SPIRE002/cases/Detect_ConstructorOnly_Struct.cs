//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct that has only a parameterless constructor and no fields.
[EnforceInitialization] //~ ERROR
public struct ConstructorOnlyStruct
{
    public ConstructorOnlyStruct() { }
}
