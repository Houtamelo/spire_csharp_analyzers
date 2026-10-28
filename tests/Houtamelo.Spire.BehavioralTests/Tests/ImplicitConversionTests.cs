using Spire.BehavioralTests.Types;
using Xunit;

namespace Spire.BehavioralTests.Tests;

public class ImplicitConversionTests
{
    // ── Basic Payload Conversions ───────────────────────────────

    [Fact]
    public void CirclePayloadImplicitlyConverts()
    {
        ImplicitConversionShape shape =
            new ImplicitConversionShapeTypes.Variants.Circle(3.5f);
        var reconstructed = FromPayload(shape.circle);

        Assert.Equal(ImplicitConversionShape.Kind.Circle, shape.kind);
        Assert.Equal(3.5f, shape.circle.radius);
        Assert.Equal(ImplicitConversionShape.Kind.Circle, reconstructed.kind);
        Assert.Equal(3.5f, reconstructed.circle.radius);

        static ImplicitConversionShape FromPayload(
            ImplicitConversionShapeTypes.Variants.Circle payload) => payload;
    }

    [Fact]
    public void RectanglePayloadImplicitlyConvertsThroughArgument()
    {
        var shape = Accept(
            new ImplicitConversionShapeTypes.Variants.Rectangle(4.5f, 7.25f));

        Assert.Equal(ImplicitConversionShape.Kind.Rectangle, shape.kind);
        Assert.Equal(4.5f, shape.rectangle.width);
        Assert.Equal(7.25f, shape.rectangle.height);

        static ImplicitConversionShape Accept(ImplicitConversionShape value) => value;
    }

    [Fact]
    public void PointPayloadImplicitlyConverts()
    {
        ImplicitConversionShape shape =
            new ImplicitConversionShapeTypes.Variants.Point();

        Assert.Equal(ImplicitConversionShape.Kind.Point, shape.kind);
        shape.point.Deconstruct();
    }

    // ── Generic Payload Conversions ─────────────────────────────

    [Fact]
    public void GenericPayloadImplicitlyConvertsForMultipleClosedTypes()
    {
        ImplicitConversionOption<int> integer =
            new ImplicitConversionOptionTypes1.Variants.Some<int>(42, 1.5f);
        ImplicitConversionOption<float> single =
            new ImplicitConversionOptionTypes1.Variants.Some<float>(2.5f, 3.5f);
        ImplicitConversionOption<int> none =
            new ImplicitConversionOptionTypes1.Variants.None<int>();

        Assert.Equal(ImplicitConversionOption<int>.Kind.Some, integer.kind);
        Assert.Equal(42, integer.some.value);
        Assert.Equal(1.5f, integer.some.scale);
        Assert.Equal(ImplicitConversionOption<float>.Kind.Some, single.kind);
        Assert.Equal(2.5f, single.some.value);
        Assert.Equal(3.5f, single.some.scale);
        Assert.Equal(ImplicitConversionOption<int>.Kind.None, none.kind);
    }

    // ── Managed and Nullable Payloads ───────────────────────────

    [Fact]
    public void NullableTupleAndManagedValuesSurviveImplicitConversion()
    {
        ImplicitConversionManaged nullText =
            new ImplicitConversionManagedTypes.Variants.Text(null);
        ImplicitConversionManaged tuple =
            new ImplicitConversionManagedTypes.Variants.Pair(("pair", 7));
        var managedPair = new ImplicitManagedPair("managed", 8);
        ImplicitConversionManaged managed =
            new ImplicitConversionManagedTypes.Variants.Managed(managedPair);
        var referencePayload = new ImplicitReferencePayload(9);
        ImplicitConversionManaged reference =
            new ImplicitConversionManagedTypes.Variants.Reference(referencePayload);

        Assert.Null(nullText.text.value);
        Assert.Equal(("pair", 7), tuple.pair.value);
        Assert.Equal("managed", managed.managed.value.Name);
        Assert.Equal(8, managed.managed.value.Count);
        Assert.Same(referencePayload, reference.reference.value);
        Assert.Equal(9, reference.reference.value.Id);
    }

    // ── Nested and Readonly Payloads ────────────────────────────

    [Fact]
    public void NestedPayloadImplicitlyConverts()
    {
        ImplicitConversionNested.Command command =
            new ImplicitConversionNested.CommandTypes.Variants.Run(2.5f);

        Assert.Equal(ImplicitConversionNested.Command.Kind.Run, command.kind);
        Assert.Equal(2.5f, command.run.speed);
    }

    [Fact]
    public void ReadonlyPayloadImplicitlyConverts()
    {
        var payload = new ImplicitConversionReadonlyTypes.Variants.Value(9);
        ImplicitConversionReadonly value = payload;

        Assert.Equal(ImplicitConversionReadonly.Kind.Value, value.kind);
        Assert.Equal(9, value.value.value);
    }
}
