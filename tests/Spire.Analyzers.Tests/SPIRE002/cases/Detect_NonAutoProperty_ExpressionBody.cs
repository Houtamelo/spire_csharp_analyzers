//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is applied to a struct whose only member is an expression-bodied property.
[MustBeInit] //~ ERROR
public struct Detect_NonAutoProperty_ExpressionBody_Struct
{
    public int Value => 42;
}
