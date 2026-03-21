//@ should_pass
// Ensure that SPIRE015 is NOT triggered when case Color.Red and case not Color.Red cover all members.
public class NoReport_SwitchStatement_NotPattern_CoversRemainder
{
    public void Method(Color color)
    {
        switch (color)
        {
            case Color.Red:
                break;
            case not Color.Red:
                break;
        }
    }
}
