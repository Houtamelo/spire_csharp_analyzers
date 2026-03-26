//@ should_fail
// SPIRE014: variant field passed as method argument with no kind guard
using System;
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct LogEntry
    {
        [Variant] public static partial LogEntry Info(string infoMessage);
        [Variant] public static partial LogEntry Error(int errorCode);
    }
    class C
    {
        void Test(LogEntry entry)
        {
            Console.WriteLine(entry.infoMessage); //~ ERROR
        }
    }
}
