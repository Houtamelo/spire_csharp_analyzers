//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() appears in a switch expression arm.
public class Detect_NewT_SwitchExpressionArm
{
    public MustInitNoCtor Method(int value)
    {
        return value switch
        {
            1 => new MustInitNoCtor(1, "one"),
            _ => new MustInitNoCtor(), //~ ERROR
        };
    }
}
