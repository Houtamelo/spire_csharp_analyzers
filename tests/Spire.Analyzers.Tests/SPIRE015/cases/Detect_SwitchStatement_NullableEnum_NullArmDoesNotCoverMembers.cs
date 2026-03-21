//@ should_fail
// Ensure that SPIRE015 IS triggered when switching on Color? handles only the null case — none of the enum members are covered.
public class Detect_SwitchStatement_NullableEnum_NullArmDoesNotCoverMembers
{
    public void Method(Color? value)
    {
        switch (value) //~ ERROR
        {
            case null:
                break;
        }
    }
}
