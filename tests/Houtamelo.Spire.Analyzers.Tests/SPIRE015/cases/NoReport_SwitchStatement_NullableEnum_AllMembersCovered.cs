//@ should_pass
// Ensure that SPIRE015 is NOT triggered when switching on Color? covers all three members AND null.
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
            case null:
                break;
        }
    }
}
