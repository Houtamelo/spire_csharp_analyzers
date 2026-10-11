using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Houtamelo.Spire.Showcase
{
    public static partial class ShowcaseAlgorithms
    {
        public static float Area(Shape shape) => shape switch
        {
            { kind: Shape.Kind.Circle, radius: var radius } => Mathf.PI * radius * radius,
            { kind: Shape.Kind.Rectangle, width: var width, height: var height } => width * height,
            { kind: Shape.Kind.Point } => 0,
        };

        public static ActorMode ReadMode(int value) => SpireEnum<ActorMode>.From(value);

        public static int CountMoves(IEnumerable<GameplayCommand> commands)
            => commands.OfKind(GameplayCommand.Kind.Move).Count();

        [InlinerStruct]
        public static int DoubleScore(int value) => value * 2;

        [InlinerStruct]
        public static int AddDoubledScore(int total, int score)
            => total + default(DoubleScoreInliner).Invoke(score);

        public static int ReduceScores(List<int> scores, int seed, [Inlinable] Func<int, int, int> fold)
        {
            foreach (var score in scores)
                seed = fold(seed, score);
            return seed;
        }

        public static int SumDoubledScores(List<int> scores)
            => ReduceScores(scores, 0, default(AddDoubledScoreInliner));
    }
}
