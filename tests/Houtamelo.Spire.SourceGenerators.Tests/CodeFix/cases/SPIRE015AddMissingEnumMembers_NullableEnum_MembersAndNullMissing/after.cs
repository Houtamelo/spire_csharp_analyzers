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
    int GetScore(Color? color) => color switch
    {
        Color.Red => 1,
        Color.Green => throw new System.NotImplementedException(),
        Color.Blue => throw new System.NotImplementedException(),
        null => throw new System.NotImplementedException(),
    };
}
