//@ should_fail
// Ensure that SPIRE015 IS triggered when None, Read, Write, ReadWrite are handled explicitly but Execute is absent.
public class Detect_SwitchStatement_Flags_MissingExecute
{
    public void Method(Permission permission)
    {
        switch (permission) //~ ERROR
        {
            case Permission.None:
                break;
            case Permission.Read:
                break;
            case Permission.Write:
                break;
            case Permission.ReadWrite:
                break;
        }
    }
}
