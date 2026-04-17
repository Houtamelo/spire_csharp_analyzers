using Houtamelo.Spire;

namespace TestNs;

public static partial class Samples
{
    [InlinerStruct]
    public static void Log(string msg) { System.Console.WriteLine(msg); }
}
