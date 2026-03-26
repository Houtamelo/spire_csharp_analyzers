//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch statement has no arms at all on Color.
public class Detect_SwitchStatement_AllMembersMissing
{
    public void Method(Color color)
    {
        switch (color) //~ ERROR
        {
        }
    }
}
