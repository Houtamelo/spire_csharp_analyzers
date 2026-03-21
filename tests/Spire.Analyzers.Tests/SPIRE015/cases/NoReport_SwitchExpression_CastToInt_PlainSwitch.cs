//@ should_pass
// Ensure that SPIRE015 is NOT triggered when ((int)color) switch { ... } is used — the switched type is int.
public class NoReport_SwitchExpression_CastToInt_PlainSwitch
{
    public string Method(Color color)
    {
        return ((int)color) switch
        {
            0 => "red",
            _ => "other",
        };
    }
}
