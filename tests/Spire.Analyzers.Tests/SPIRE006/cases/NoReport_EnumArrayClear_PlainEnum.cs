//@ should_pass
// Ensure that SPIRE006 is NOT triggered when Array.Clear is called on a PlainEnum[] because the enum is not marked with [EnforceInitialization].
public class NoReport_EnumArrayClear_PlainEnum
{
    public void Method()
    {
        var arr = new[] { PlainEnum.A, PlainEnum.B, PlainEnum.C };
        Array.Clear(arr);
    }
}
