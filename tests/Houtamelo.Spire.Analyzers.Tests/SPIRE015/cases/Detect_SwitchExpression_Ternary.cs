//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch expression on Color appears as a branch of a ternary conditional.
public class Detect_SwitchExpression_Ternary
{
    public string Method(Color color, bool useSwitch)
    {
        return useSwitch
            ? color switch //~ ERROR
              {
                  Color.Red => "red",
              }
            : "static";
    }
}
