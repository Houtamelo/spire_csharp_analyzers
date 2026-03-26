//@ should_fail
// Ensure that SPIRE015 IS triggered when a switch statement on SingleMember has an empty body (no arms).
public class Detect_SwitchStatement_SingleMember_NoArms
{
    public void Method(SingleMember value)
    {
        switch (value) //~ ERROR
        {
        }
    }
}
