//@ should_fail
// Ensure that SPIRE015 IS triggered when every arm in the switch statement has a when guard so no member counts as covered.
public class Detect_SwitchStatement_WhenGuard_AllArmsGuarded
{
    public void Method(Color color, bool condition)
    {
        switch (color) //~ ERROR
        {
            case Color.Red when condition:
                break;
            case Color.Green when condition:
                break;
            case Color.Blue when condition:
                break;
        }
    }
}
