//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a struct with only a static method and no fields.
[MustBeInit] //~ ERROR
public struct StaticMethodOnlyStruct
{
    public static int Create() => 0;
}
