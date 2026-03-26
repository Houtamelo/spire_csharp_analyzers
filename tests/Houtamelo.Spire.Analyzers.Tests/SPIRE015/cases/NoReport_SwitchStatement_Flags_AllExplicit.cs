//@ should_pass
// Ensure that SPIRE015 is NOT triggered when all five Permission members are explicitly handled.
public class NoReport_SwitchStatement_Flags_AllExplicit
{
    public void Method(Permission permission)
    {
        switch (permission)
        {
            case Permission.None:
                break;
            case Permission.Read:
                break;
            case Permission.Write:
                break;
            case Permission.ReadWrite:
                break;
            case Permission.Execute:
                break;
        }
    }
}
