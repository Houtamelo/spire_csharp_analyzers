//@ should_fail
// Ensure that SPIRE015 IS triggered when only Permission.ReadWrite is handled (covering None, Read, Write via bitwise-subset) but Execute remains uncovered.
public class Detect_SwitchExpression_Flags_OnlyReadWrite
{
    public string Method(Permission permission)
    {
        return permission switch //~ ERROR
        {
            Permission.ReadWrite => "readwrite",
        };
    }
}
