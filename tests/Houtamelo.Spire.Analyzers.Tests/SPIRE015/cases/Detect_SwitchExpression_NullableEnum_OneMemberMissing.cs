//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch expression on Color? covers Red but not Green or Blue.
public class Detect_SwitchExpression_NullableEnum_OneMemberMissing
{
    public string Method(Color? value)
    {
        return value switch //~ ERROR
        {
            Color.Red => "red",
        };
    }
}
