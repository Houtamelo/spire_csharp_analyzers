//@ should_pass
// Ensure that SPIRE001 is NOT triggered for empty array of [MustBeInit] class.
#nullable enable
public class NoReport_EmptyArrayClass
{
    void Ok()
    {
        var arr = new MustInitClass[0];
    }
}
