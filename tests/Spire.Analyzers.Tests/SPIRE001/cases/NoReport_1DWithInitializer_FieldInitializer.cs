//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an array with an explicit initializer.
public class NoReport_1DWithInitializer_FieldInitializer
{
    private EnforceInitializationStruct[] _arr = new EnforceInitializationStruct[] { new EnforceInitializationStruct(1), new EnforceInitializationStruct(2) };
}
