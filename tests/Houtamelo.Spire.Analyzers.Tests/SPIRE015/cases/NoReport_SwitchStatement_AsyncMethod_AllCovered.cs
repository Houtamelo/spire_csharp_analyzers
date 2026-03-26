//@ should_pass
// Ensure that SPIRE015 is NOT triggered when a switch statement inside an async method covers all Color members.
public class NoReport_SwitchStatement_AsyncMethod_AllCovered
{
    public async Task Method(Color value)
    {
        await Task.Yield();
        switch (value)
        {
            case Color.Red:
                break;
            case Color.Green:
                break;
            case Color.Blue:
                break;
        }
    }
}
