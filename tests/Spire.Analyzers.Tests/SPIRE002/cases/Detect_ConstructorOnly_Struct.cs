//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a struct that has only a parameterless constructor and no fields.
[MustBeInit] //~ ERROR
public struct ConstructorOnlyStruct
{
    public ConstructorOnlyStruct() { }
}
