//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using a const zero variable as array size.
public class NoReport_ConstZeroVariable
{
    public void Method()
    {
        const int n = 0;
        var arr = new MustInitStruct[n];
    }
}
