//@ should_pass
// Ensure that SPIRE015 is NOT triggered when switching on Empty (no members), even with an empty switch body.
public class NoReport_SwitchStatement_EmptyEnum
{
    public void Method(Empty value)
    {
        switch (value)
        {
        }
    }
}
