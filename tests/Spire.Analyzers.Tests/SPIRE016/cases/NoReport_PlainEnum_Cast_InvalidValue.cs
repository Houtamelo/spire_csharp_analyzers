//@ should_pass
// Ensure that SPIRE016 is NOT triggered when casting an integer to an enum not marked with [MustBeInit].
public class NoReport_PlainEnum_Cast_InvalidValue
{
    void M()
    {
        PlainEnum a = (PlainEnum)999;
        PlainEnum b = (PlainEnum)(-1);
        PlainEnum c = (PlainEnum)0;
        PlainEnum d = (PlainEnum)42;

        int x = 100;
        PlainEnum e = (PlainEnum)x;
    }
}
