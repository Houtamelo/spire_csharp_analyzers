//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a variable-size array of a plain struct (no [EnforceInitialization]).
public class NoReport_PlainStruct_VariableSize
{
    public void Method(int n)
    {
        var arr = new PlainStruct[n];
    }
}
