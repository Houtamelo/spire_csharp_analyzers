//@ should_pass
// Ensure that SPIRE015 is NOT triggered when Permission.ReadWrite (covering None, Read, Write via bitwise-subset) and Permission.Execute are handled.
public class NoReport_SwitchStatement_Flags_ReadWriteCoversComponents
{
    public void Method(Permission permission)
    {
        switch (permission)
        {
            case Permission.ReadWrite:
                break;
            case Permission.Execute:
                break;
        }
    }
}
