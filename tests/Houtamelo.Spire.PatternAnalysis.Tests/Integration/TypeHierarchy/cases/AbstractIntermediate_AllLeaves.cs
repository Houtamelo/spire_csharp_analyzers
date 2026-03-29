//@ exhaustive
// Abstract intermediate skipped — only concrete leaves count
#nullable enable

[EnforceExhaustiveness]
public abstract class Vehicle2 { }
public abstract class MotorVehicle : Vehicle2 { }
public sealed class Car2 : MotorVehicle { }
public sealed class Truck : MotorVehicle { }
public sealed class Bicycle : Vehicle2 { }

public class AbstractIntermediate_AllLeaves
{
    public int Test(Vehicle2 v) => v switch
    {
        Car2 => 1,
        Truck => 2,
        Bicycle => 3,
    };
}
