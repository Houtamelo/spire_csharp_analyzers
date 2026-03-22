//@ should_pass
// Class union switch expression using type patterns, all covered — no diagnostic
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public abstract partial class Animal
    {
        public sealed partial class Dog : Animal
        {
            public string Breed { get; }
            public Dog(string breed) { Breed = breed; }
        }
        public sealed partial class Cat : Animal
        {
            public bool IsIndoor { get; }
            public Cat(bool isIndoor) { IsIndoor = isIndoor; }
        }
        public sealed partial class Bird : Animal
        {
            public string Species { get; }
            public Bird(string species) { Species = species; }
        }
    }

    class PassAnimalConsumer
    {
        string Describe(Animal a) => a switch
        {
            Animal.Dog d => $"dog:{d.Breed}",
            Animal.Cat c => $"cat:{c.IsIndoor}",
            Animal.Bird b => $"bird:{b.Species}",
        };
    }
}
