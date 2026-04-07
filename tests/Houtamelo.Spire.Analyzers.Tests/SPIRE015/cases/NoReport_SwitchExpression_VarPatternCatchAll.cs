//@ should_pass
// SPIRE015 does NOT fire when the catch-all is a `var x` declaration pattern.
public class NoReport_SwitchExpression_VarPatternCatchAll
{
    public string Method(Color color)
    {
        return color switch
        {
            Color.Red => "red",
            var c => c.ToString(),
        };
    }
}
