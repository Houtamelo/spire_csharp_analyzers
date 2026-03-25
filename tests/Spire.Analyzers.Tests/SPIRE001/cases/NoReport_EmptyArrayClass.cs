//@ should_pass
// Ensure that SPIRE001 is NOT triggered for empty array of [EnforceInitialization] class.
#nullable enable
public class NoReport_EmptyArrayClass
{
    void Ok()
    {
        var arr = new EnforceInitializationClass[0];
    }
}
