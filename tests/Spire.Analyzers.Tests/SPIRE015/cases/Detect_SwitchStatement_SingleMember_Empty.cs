//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch statement on SingleMember (one-member enum) has no arms.
public class Detect_SwitchStatement_SingleMember_Empty
{
    public void Method(SingleMember value)
    {
        switch (value) //~ ERROR
        {
        }
    }
}
