//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() appears in a switch expression arm.
public class Detect_NewT_SwitchExpressionArm
{
    public EnforceInitializationNoCtor Method(int value)
    {
        return value switch
        {
            1 => new EnforceInitializationNoCtor(1, "one"),
            _ => new EnforceInitializationNoCtor(), //~ ERROR
        };
    }
}
