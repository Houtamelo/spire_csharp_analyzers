//@ should_pass
// Ensure that SPIRE001 is NOT triggered when array element type is nullable [EnforceInitialization] class.
#nullable enable
public class NoReport_NullableArrayClass
{
    void Ok()
    {
        var arr = new EnforceInitializationClass?[5];
    }
}
