#nullable enable

using System;
using System.Collections.Generic;
using Houtamelo.Spire.Showcase;
using NUnit.Framework;
using UnityEngine;

namespace Houtamelo.Spire.Showcase.Tests
{
    public sealed class ShowcaseRuntimeTests
    {
        [Test]
        public void UnionFactories_PreserveKindsAndPayloads()
        {
            var idle = GameplayCommand.Idle();
            var move = GameplayCommand.Move(new Vector2(3f, 4f), 2f);
            var speak = GameplayCommand.Speak("hello");
            var effect = GameplayCommand.ApplyEffect(new DamageEffect(10));

            ShowcaseTestSupport.AssertIdleCommand(idle);
            ShowcaseTestSupport.AssertMoveCommand(move, new Vector2(3f, 4f), 2f);
            ShowcaseTestSupport.AssertSpeakCommand(speak, "hello");

            var effectValue = ShowcaseTestSupport.GetEffect(effect);
            Assert.That(effectValue, Is.TypeOf<DamageEffect>());
            Assert.That(((DamageEffect)effectValue!).amount, Is.EqualTo(10));
        }

        [Test]
        public void Effects_ClampHealthAtTheDeclaredBounds()
        {
            var damage = new DamageEffect(30);
            var healing = new HealingEffect(30);

            Assert.That(damage.Apply(100), Is.EqualTo(70));
            Assert.That(damage.Apply(5), Is.EqualTo(0));
            Assert.That(healing.Apply(70), Is.EqualTo(100));
            Assert.That(healing.Apply(100), Is.EqualTo(100));
        }

        [Test]
        public void ShowcaseAlgorithms_AreaReadsKindsAndCountsMoves()
        {
            Assert.That(ShowcaseAlgorithms.Area(Shape.Circle(2f)), Is.EqualTo(Math.PI * 4d).Within(0.00001d));
            Assert.That(ShowcaseAlgorithms.Area(Shape.Rectangle(3f, 4f)), Is.EqualTo(12d).Within(0.00001d));
            Assert.That(ShowcaseAlgorithms.Area(Shape.Point()), Is.EqualTo(0d).Within(0.00001d));

            Assert.That(ShowcaseAlgorithms.ReadMode(1), Is.EqualTo(ActorMode.Idle));
            Assert.That(ShowcaseAlgorithms.ReadMode(2), Is.EqualTo(ActorMode.Acting));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShowcaseAlgorithms.ReadMode(99));

            var commands = new List<GameplayCommand>
        {
            GameplayCommand.Idle(),
            GameplayCommand.Move(new Vector2(1f, 2f), 0.5f),
            GameplayCommand.Speak("one"),
            GameplayCommand.Move(new Vector2(4f, 5f), 3f),
            GameplayCommand.ApplyEffect(null),
        };

            Assert.That(ShowcaseAlgorithms.CountMoves(commands), Is.EqualTo(2));
        }

        [Test]
        public void GeneratedInliners_ForwardCallsAndSelectTheInlinableTwin()
        {
            var doubleScore = default(ShowcaseAlgorithms.DoubleScoreInliner);
            Assert.That(doubleScore.Invoke(21), Is.EqualTo(42));
            Assert.That(ShowcaseAlgorithms.DoubleScore(21), Is.EqualTo(42));

            var addDoubledScore = default(ShowcaseAlgorithms.AddDoubledScoreInliner);
            Assert.That(addDoubledScore.Invoke(10, 4), Is.EqualTo(18));

            var scores = new List<int> { 1, 2, 3, 4 };
            Assert.That(ShowcaseAlgorithms.SumDoubledScores(scores), Is.EqualTo(20));
        }

        [Test]
        public void ShowcaseController_ExecutesCommandsAndResetsSession()
        {
            var gameObject = new GameObject("ShowcaseControllerTests");
            try
            {
                var controller = gameObject.AddComponent<ShowcaseController>();

                controller.ResetSession();
                Assert.That(controller.Health, Is.EqualTo(100));
                Assert.That(controller.Position, Is.EqualTo(Vector2.zero));
                Assert.That(controller.LastSpeed, Is.EqualTo(0f));
                Assert.That(controller.LastMessage, Is.EqualTo(string.Empty));
                Assert.That(controller.Mode, Is.EqualTo(ActorMode.Idle));

                controller.Execute(GameplayCommand.Move(new Vector2(3f, 4f), 2f));
                Assert.That(controller.Position, Is.EqualTo(new Vector2(3f, 4f)));
                Assert.That(controller.LastSpeed, Is.EqualTo(2f));
                Assert.That(controller.Mode, Is.EqualTo(ActorMode.Acting));

                controller.Execute(GameplayCommand.Speak("hello"));
                Assert.That(controller.LastMessage, Is.EqualTo("hello"));

                controller.Execute(GameplayCommand.ApplyEffect(new DamageEffect(30)));
                Assert.That(controller.Health, Is.EqualTo(70));

                controller.Execute(GameplayCommand.ApplyEffect(null));
                Assert.That(controller.Health, Is.EqualTo(70));

                controller.Execute(GameplayCommand.Idle());
                Assert.That(controller.Mode, Is.EqualTo(ActorMode.Idle));

                controller.ResetSession();
                Assert.That(controller.Health, Is.EqualTo(100));
                Assert.That(controller.Position, Is.EqualTo(Vector2.zero));
                Assert.That(controller.LastSpeed, Is.EqualTo(0f));
                Assert.That(controller.LastMessage, Is.EqualTo(string.Empty));
                Assert.That(controller.Mode, Is.EqualTo(ActorMode.Idle));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

    }
}
