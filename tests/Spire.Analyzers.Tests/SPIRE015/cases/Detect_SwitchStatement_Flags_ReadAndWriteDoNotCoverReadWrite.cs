//@ should_fail
// Ensure that SPIRE015 IS triggered when Permission.Read and Permission.Write are handled individually but ReadWrite, Execute, and None are missing.
public class Detect_SwitchStatement_Flags_ReadAndWriteDoNotCoverReadWrite
{
    public void Method(Permission permission)
    {
        switch (permission) //~ ERROR
        {
            case Permission.Read:
                break;
            case Permission.Write:
                break;
        }
    }
}
