//@ should_pass
// Ensure that SPIRE004 is NOT triggered when a parameterized constructor is used as a field initializer for a [MustBeInit] struct.
public class NoReport_ParameterizedCtor_FieldInitializer
{
    private MustInitNoCtor _field = new MustInitNoCtor(42, "hello");
}
