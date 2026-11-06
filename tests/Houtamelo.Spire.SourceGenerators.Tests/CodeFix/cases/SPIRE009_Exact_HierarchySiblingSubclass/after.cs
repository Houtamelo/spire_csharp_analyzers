#nullable enable
using Houtamelo.Spire;

namespace Animals
{
    [EnforceExhaustiveness]
    public abstract class Animal
    {
    }

    public sealed class Dog : Animal
    {
    }

    public sealed class Cat : Animal
    {
    }

    public sealed class Bird : Animal
    {
    }
}

class Consumer
{
    int Read(Animals.Animal animal) => animal switch
    {
        global::Animals.Dog => 1,
        global::Animals.Cat => 2,
        Animals.Bird => throw new System.NotImplementedException(),
    };
}
