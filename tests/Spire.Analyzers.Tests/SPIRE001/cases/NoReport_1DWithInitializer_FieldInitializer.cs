//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an array with an explicit initializer.
public class NoReport_1DWithInitializer_FieldInitializer
{
    private MustInitStruct[] _arr = new MustInitStruct[] { new MustInitStruct(1), new MustInitStruct(2) };
}
