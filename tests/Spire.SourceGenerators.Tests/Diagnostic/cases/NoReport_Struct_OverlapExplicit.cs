//@ should_pass
// No diagnostics: Overlap layout on non-generic struct is valid
using Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.Overlap)]
    partial struct Command
    {
        [Variant] public static partial Command Move(int dx, int dy);
        [Variant] public static partial Command Rotate(float angle);
        [Variant] public static partial Command Scale(float factor);
    }
}
