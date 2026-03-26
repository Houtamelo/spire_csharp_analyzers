//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating a sized array with an explicit initializer.
public class NoReport_1DSizedWithInitializer_LocalVariable
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[2] { new EnforceInitializationStruct(1), new EnforceInitializationStruct(2) };
    }
}
