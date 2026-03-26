//@ should_pass
// Ensure that SPIRE015 is NOT triggered when SingleMember.Only is the sole arm in a switch statement.
public class NoReport_SwitchStatement_SingleMember_Covered
{
    public void Method(SingleMember value)
    {
        switch (value)
        {
            case SingleMember.Only:
                break;
        }
    }
}
