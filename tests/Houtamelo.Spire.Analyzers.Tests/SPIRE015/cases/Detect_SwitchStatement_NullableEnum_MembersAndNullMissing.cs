//@ should_fail
// Color? switch missing both Green member and null — SPIRE015
public class Detect_SwitchStatement_NullableEnum_MembersAndNullMissing
{
    public void Method(Color? value)
    {
        switch (value) //~ ERROR
        {
            case Color.Red:
                break;
            case Color.Blue:
                break;
        }
    }
}
