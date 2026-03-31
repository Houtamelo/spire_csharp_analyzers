//@ should_pass
#nullable enable
// Record union with var-declared local from non-nullable return — null case NOT required
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial record GameState
    {
        public sealed partial record MainMenu(string title) : GameState;
        public sealed partial record Playing(int level) : GameState;
    }

    class VarLocalConsumer
    {
        GameState GetState() => new GameState.MainMenu("test");

        void Handle()
        {
            var state = GetState();
            switch (state)
            {
                case GameState.MainMenu m:
                    System.Console.WriteLine(m.title);
                    break;
                case GameState.Playing p:
                    System.Console.WriteLine(p.level);
                    break;
            }
        }
    }
}
