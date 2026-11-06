using Houtamelo.Spire;

[EnforceExhaustiveness]
enum Color
{
    Red,
    Green,
    Blue,
}

class Consumer
{
    int GetScore(Color color) => color switch
    {
        Color.Red => 1,
        Color.Green => 2,
        Color.Blue => throw new System.NotImplementedException(),
    };
}
