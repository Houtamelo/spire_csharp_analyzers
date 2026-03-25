//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a struct whose only member is an expression-bodied property.
[EnforceInitialization] //~ ERROR
public struct Detect_NonAutoProperty_ExpressionBody_Struct
{
    public int Value => 42;
}
