//@ should_pass
// Not a discriminated union — no diagnostic
namespace TestNs
{
    enum Color { Red, Green, Blue }
    class C
    {
        int Test(Color c) => c switch
        {
            Color.Red => 1,
            _ => 0,
        };
    }
}
