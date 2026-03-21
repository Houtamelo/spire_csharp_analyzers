//@ should_fail
// Ensure that SPIRE015 IS triggered when switching on Color? (nullable Color) with Red covered but Green and Blue missing.
public class Detect_SwitchStatement_NullableEnum_OneMemberMissing
{
    public void Method(Color? value)
    {
        switch (value) //~ ERROR
        {
            case Color.Red:
                break;
        }
    }
}
