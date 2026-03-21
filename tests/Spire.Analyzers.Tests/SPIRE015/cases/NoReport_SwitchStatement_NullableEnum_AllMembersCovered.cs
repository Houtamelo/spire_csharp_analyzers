//@ should_pass
// Ensure that SPIRE015 is NOT triggered when switching on Color? covers all three members (Red, Green, Blue) regardless of whether null is handled.
public class NoReport_SwitchStatement_NullableEnum_AllMembersCovered
{
    public void Method(Color? value)
    {
        switch (value)
        {
            case Color.Red:
                break;
            case Color.Green:
                break;
            case Color.Blue:
                break;
        }
    }
}
