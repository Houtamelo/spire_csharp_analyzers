// Sources shared by the direct-checker and compiler/suppressor integration tests.
namespace Houtamelo.Spire.TestFixtures.NestedRecursive;

internal static class NestedRecursiveSources
{
    public const string NestedBoolComplete = """
        #nullable enable
        public sealed class Child
        {
            public bool Flag { get; }
            public Child(bool flag) => Flag = flag;
        }
        public sealed class Root
        {
            public Child Child { get; }
            public Root(Child child) => Child = child;
        }
        public static class C
        {
            public static int M(Root root) => root switch
            {
                { Child: { Flag: true } } => 1,
                { Child: { Flag: false } } => 2,
            };
        }
        """;

    public const string NestedEnumMissing = """
        public enum Kind { A, B, C }
        public sealed class Child
        {
            public Kind Value { get; }
            public Child(Kind value) => Value = value;
        }
        public sealed class Root
        {
            public Child Child { get; }
            public Root(Child child) => Child = child;
        }
        public static class C
        {
            public static int M(Root root) => root switch
            {
                { Child: { Value: Kind.A } } => 1,
                { Child: { Value: Kind.B } } => 2,
            };
        }
        """;

    public const string NullableParentComplete = """
        #nullable enable
        public sealed class Child
        {
            public bool Flag { get; }
            public Child(bool flag) => Flag = flag;
        }
        public sealed class Root
        {
            public Child? Child { get; }
            public Root(Child? child) => Child = child;
        }
        public static class C
        {
            public static int M(Root root) => root switch
            {
                { Child: null } => 0,
                { Child: { Flag: true } } => 1,
                { Child: { Flag: false } } => 2,
            };
        }
        """;

    public const string NullableRootComplete = """
        #nullable enable
        public sealed class Root
        {
            public bool Flag { get; }
            public Root(bool flag) => Flag = flag;
        }
        public static class C
        {
            public static int M(Root? root) => root switch
            {
                null => 0,
                { Flag: true } => 1,
                { Flag: false } => 2,
            };
        }
        """;

    public const string NullableRootMissingNull = """
        #nullable enable
        public sealed class Root
        {
            public bool Flag { get; }
            public Root(bool flag) => Flag = flag;
        }
        public static class C
        {
            public static int M(Root? root) => root switch
            {
                { Flag: true } => 1,
                { Flag: false } => 2,
            };
        }
        """;

    public const string SiblingMissing = """
        public sealed class Child
        {
            public bool Flag { get; }
            public Child(bool flag) => Flag = flag;
        }
        public sealed class Root
        {
            public Child Left { get; }
            public Child Right { get; }
            public Root(Child left, Child right)
            {
                Left = left;
                Right = right;
            }
        }
        public static class C
        {
            public static int M(Root root) => root switch
            {
                { Left: { Flag: true }, Right: { Flag: true } } => 1,
                { Left: { Flag: true }, Right: { Flag: false } } => 2,
                { Left: { Flag: false }, Right: { Flag: true } } => 3,
            };
        }
        """;

    public const string CorrelatedMissing = """
        public enum Kind { A, B }
        public sealed class Root
        {
            public Kind Kind { get; }
            public bool Enabled { get; }
            public Root(Kind kind, bool enabled)
            {
                Kind = kind;
                Enabled = enabled;
            }
        }
        public static class C
        {
            public static int M(Root root) => root switch
            {
                { Kind: Kind.A, Enabled: true } => 1,
                { Kind: Kind.A, Enabled: false } => 2,
                { Kind: Kind.B, Enabled: true } => 3,
            };
        }
        """;

    public const string DerivedMissing = """
        #nullable enable
        using Houtamelo.Spire;
        [EnforceExhaustiveness]
        public abstract class Node { }
        public sealed class First : Node
        {
            public bool Flag { get; }
            public First(bool flag) => Flag = flag;
        }
        public sealed class Second : Node
        {
            public bool Flag { get; }
            public Second(bool flag) => Flag = flag;
        }
        public static class C
        {
            public static int M(Node node) => node switch
            {
                First { Flag: true } => 1,
                First { Flag: false } => 2,
                Second { Flag: true } => 3,
            };
        }
        """;

    public const string DerivedGuardMissing = """
        #nullable enable
        using Houtamelo.Spire;
        [EnforceExhaustiveness]
        public abstract class Node { }
        public sealed class First : Node
        {
            public bool Flag { get; }
            public First(bool flag) => Flag = flag;
        }
        public sealed class Second : Node
        {
            public bool Flag { get; }
            public Second(bool flag) => Flag = flag;
        }
        public static class C
        {
            public static int M(Node node) => node switch
            {
                First { Flag: true } when true => 1,
                First { Flag: false } => 2,
                Second { Flag: true } => 3,
                Second { Flag: false } => 4,
            };
        }
        """;

    public const string UnsupportedDeconstruction = """
        public sealed class Pair
        {
            public void Deconstruct(out bool left, out bool right)
            {
                left = false;
                right = false;
            }
        }
        public static class C
        {
            public static int M(Pair pair) => pair switch
            {
                (true, true) => 1,
                (true, false) => 2,
                (false, true) => 3,
            };
        }
        """;

    public const string ExplicitDiscard = """
        public sealed class Root
        {
            public bool Flag { get; }
            public Root(bool flag) => Flag = flag;
        }
        public static class C
        {
            public static int M(Root root) => root switch
            {
                { Flag: true } => 1,
                _ => 2,
            };
        }
        """;

    public const string UnsupportedDeconstructionWithDiscardComplete = """
        public sealed class Pair
        {
            public void Deconstruct(out bool left, out bool right)
            {
                left = false;
                right = false;
            }
        }
        public static class C
        {
            public static int M(Pair pair) => pair switch
            {
                (true, true) => 1,
                _ => 2,
            };
        }
        """;

    public const string ManualStructUnionDeconstructionMissing = """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public readonly struct ManualCommand
        {
            public enum Kind { A, B }

            public Kind kind { get; }
            private readonly bool _flag;

            public ManualCommand(Kind kind, bool flag)
            {
                this.kind = kind;
                _flag = flag;
            }

            public void Deconstruct(out Kind nestedKind, out bool flag)
            {
                nestedKind = kind;
                flag = _flag;
            }
        }
        public static class C
        {
            public static int Read(ManualCommand command) => command switch
            {
                (ManualCommand.Kind.A, true) => 1,
                (ManualCommand.Kind.B, _) => 2,
            };
        }
        """;

    public const string ManualNullableStructUnionDeconstructionMissing = """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public readonly struct ManualNullableCommand
        {
            public enum Kind { A, B }

            public Kind kind { get; }
            private readonly bool _flag;

            public ManualNullableCommand(Kind kind, bool flag)
            {
                this.kind = kind;
                _flag = flag;
            }

            public void Deconstruct(out Kind nestedKind, out bool flag)
            {
                nestedKind = kind;
                flag = _flag;
            }
        }
        public static class C
        {
            public static int Read(ManualNullableCommand? command) => command switch
            {
                null => 0,
                (ManualNullableCommand.Kind.A, true) => 1,
                (ManualNullableCommand.Kind.B, _) => 2,
            };
        }
        """;

    public const string NegatedEnumComplementMissing = """
        public enum E { A, B }
        public static class C
        {
            public static int Read(E value) => value switch
            {
                not (E.A or E.B) => 1,
            };
        }
        """;

    public const string GuardedNested = """
        public sealed class Child
        {
            public bool Flag { get; }
            public Child(bool flag) => Flag = flag;
        }
        public sealed class Root
        {
            public Child Child { get; }
            public Root(Child child) => Child = child;
        }
        public static class C
        {
            private static bool Guard() => true;

            public static int M(Root root) => root switch
            {
                { Child: { Flag: true } } when Guard() => 1,
            };
        }
        """;

    public const string NumericBoolCorrelationDoubleMissing = """
        public static class C
        {
            public static int Read(double value, bool enabled) => (value, enabled) switch
            {
                (<= 5d, true) => 1,
                (>= 3d, false) => 2,
            };
        }
        """;

    public const string NumericBoolCorrelationFloatMissing = """
        public static class C
        {
            public static int Read(float value, bool enabled) => (value, enabled) switch
            {
                (<= 5f, true) => 1,
                (>= 3f, false) => 2,
            };
        }
        """;

    public const string ExtendedPropertySiblingSharedLeafMissing = """
        public sealed class Leaf
        {
            public bool Flag { get; }
            public Leaf(bool flag) => Flag = flag;
        }
        public sealed class Root
        {
            public Leaf Left { get; }
            public Leaf Right { get; }
            public Root(Leaf left, Leaf right)
            {
                Left = left;
                Right = right;
            }
        }
        public static class C
        {
            public static int Read(Root root) => root switch
            {
                { Left.Flag: true } => 1,
                { Right.Flag: true } => 2,
            };
        }
        """;

    public const string ExtendedPropertyNullableSiblingMissing = """
        #nullable enable
        public sealed class Leaf
        {
            public bool Flag { get; }
            public Leaf(bool flag) => Flag = flag;
        }
        public sealed class Root
        {
            public Leaf? Left { get; }
            public Leaf? Right { get; }
            public Root(Leaf? left, Leaf? right)
            {
                Left = left;
                Right = right;
            }
        }
        public static class C
        {
            public static int Read(Root? root) => root switch
            {
                null => 0,
                { Left.Flag: true } => 1,
                { Right.Flag: true } => 2,
            };
        }
        """;

    public const string NestedStringLiteralMissing = """
        public sealed class Inner
        {
            public string Text { get; }
            public Inner(string text) => Text = text;
        }
        public sealed class Root
        {
            public Inner Inner { get; }
            public Root(Inner inner) => Inner = inner;
        }
        public static class C
        {
            public static int Read(Root root) => root switch
            {
                { Inner: { Text: "one" } } => 1,
            };
        }
        """;

    public const string GeneratedStructCommandOrWrappedMissing = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command A(bool flag);
            [Variant] public static partial Command B();
        }
        public static class C
        {
            public static int Read(Command command) => command switch
            {
                { kind: Command.Kind.A, a: { flag: true } }
                    or { kind: Command.Kind.B, b: var _ } => 1,
            };
        }
        """;

    public const string GeneratedStructCommandOrWrappedDiscardComplete = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command A(bool flag);
            [Variant] public static partial Command B();
        }
        public static class C
        {
            public static int Read(Command command) => command switch
            {
                { kind: Command.Kind.A, a: { flag: true } }
                    or { kind: Command.Kind.B, b: var _ } => 1,
                _ => 2,
            };
        }
        """;

    public const string EnforcedHierarchyPositionalDeconstructMissing = """
        #nullable enable
        using Houtamelo.Spire;
        [EnforceExhaustiveness]
        public abstract class Node { }
        public sealed class Leaf : Node
        {
            public bool Flag { get; }
            public Leaf(bool flag) => Flag = flag;
            public void Deconstruct(out bool flag) => flag = Flag;
        }
        public sealed class Other : Node { }
        public static class C
        {
            public static int Read(Node node) => node switch
            {
                Leaf(true) => 1,
                Other => 2,
            };
        }
        """;

    public const string EnforcedHierarchyPositionalAndPropertyComplete = """
        #nullable enable
        using Houtamelo.Spire;
        [EnforceExhaustiveness]
        public abstract class Node { }
        public sealed class Leaf : Node
        {
            public bool Flag { get; }
            public Leaf(bool flag) => Flag = flag;
            public void Deconstruct(out bool flag) => flag = Flag;
        }
        public sealed class Other : Node { }
        public static class C
        {
            public static int Read(Node node) => node switch
            {
                Leaf(true) => 1,
                Leaf { Flag: false } => 2,
                Other => 3,
            };
        }
        """;

    public const string RuntimeBoolNullableSiblingComplete = """
        #nullable enable
        public sealed class Child
        {
            public bool Flag { get; }
            public Child(bool flag) => Flag = flag;
        }
        public sealed class Root
        {
            public Child? Child { get; }
            public Child Left { get; }
            public Child Right { get; }
            public Root(Child? child, Child left, Child right)
            {
                Child = child;
                Left = left;
                Right = right;
            }
        }
        public static class C
        {
            public static int Nullable(bool? flag) => new Root(
                flag.HasValue ? new Child(flag.Value) : null,
                new Child(false),
                new Child(false)) switch
            {
                { Child: null } => 0,
                { Child: { Flag: true } } => 1,
                { Child: { Flag: false } } => 2,
            };

            public static int Sibling(bool left, bool right) => new Root(
                new Child(false),
                new Child(left),
                new Child(right)) switch
            {
                { Left: { Flag: true }, Right: { Flag: true } } => 11,
                { Left: { Flag: true }, Right: { Flag: false } } => 12,
                { Left: { Flag: false }, Right: { Flag: true } } => 13,
                { Left: { Flag: false }, Right: { Flag: false } } => 14,
            };
        }
        """;

    public const string RuntimeEnumDerivedComplete = """
        #nullable enable
        using Houtamelo.Spire;
        public enum Kind { A, B, C }
        public sealed class Child
        {
            public Kind Value { get; }
            public Child(Kind value) => Value = value;
        }
        public sealed class Root
        {
            public Child Child { get; }
            public Root(Child child) => Child = child;
        }
        [EnforceExhaustiveness]
        public abstract class Node { }
        public sealed class First : Node
        {
            public bool Flag { get; }
            public First(bool flag) => Flag = flag;
        }
        public sealed class Second : Node
        {
            public bool Flag { get; }
            public Second(bool flag) => Flag = flag;
        }
        public static class C
        {
            public static int M(Kind value) => new Root(new Child(value)) switch
            {
                { Child: { Value: Kind.A } } => 1,
                { Child: { Value: Kind.B } } => 2,
                { Child: { Value: Kind.C } } => 3,
            };

            public static int D(int which, bool flag)
            {
                Node node = which == 0 ? new First(flag) : new Second(flag);
                return node switch
                {
                    First { Flag: true } => 11,
                    First { Flag: false } => 12,
                    Second { Flag: true } => 13,
                    Second { Flag: false } => 14,
                };
            }
        }
        """;

    public const string MissingNullableParent = """
        #nullable enable
        public sealed class Child
        {
            public bool Flag { get; }
            public Child(bool flag) => Flag = flag;
        }
        public sealed class Root
        {
            public Child? Child { get; }
            public Root(Child? child) => Child = child;
        }
        public static class C
        {
            public static int M(Root root) => root switch
            {
                { Child: { Flag: true } } => 1,
                { Child: { Flag: false } } => 2,
            };
        }
        """;

    public const string MissingBool = """
        public sealed class Child
        {
            public bool Flag { get; }
            public Child(bool flag) => Flag = flag;
        }
        public sealed class Root
        {
            public Child Child { get; }
            public Root(Child child) => Child = child;
        }
        public static class C
        {
            public static int M(Root root) => root switch
            {
                { Child: { Flag: true } } => 1,
            };
        }
        """;

    public const string RestoredNullableParentComplete = NullableParentComplete;

    public const string GeneratedStructCommandMissing = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command A(bool flag);
            [Variant] public static partial Command B();
        }
        public static class C
        {
            public static int Read(Command command) => command switch
            {
                { kind: Command.Kind.A, a: { flag: true } } => 1,
                { kind: Command.Kind.B, b: var _ } => 2,
            };
        }
        """;

    public const string GeneratedStructCommandComplete = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command A(bool flag);
            [Variant] public static partial Command B();
        }
        public static class C
        {
            public static int Read(Command command) => command switch
            {
                { kind: Command.Kind.A, a: { flag: true } } => 1,
                { kind: Command.Kind.A, a: { flag: false } } => 2,
                { kind: Command.Kind.B, b: var _ } => 3,
            };
        }
        """;

    public const string GeneratedCommandPayloadEnumMissing = """
        using Houtamelo.Spire;
        public enum Color { Red, Green, Blue }
        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command A(Color color);
            [Variant] public static partial Command B();
        }
        public static class C
        {
            public static int Read(Command command) => command switch
            {
                { kind: Command.Kind.A, a: { color: Color.Red } } => 1,
                { kind: Command.Kind.B, b: var _ } => 2,
            };
        }
        """;

    public const string GeneratedCommandPayloadBoolManyMissing = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command A(bool first, bool second);
            [Variant] public static partial Command B();
        }
        public static class C
        {
            public static int Read(Command command) => command switch
            {
                { kind: Command.Kind.A, a: { first: true, second: true } } => 1,
                { kind: Command.Kind.B, b: var _ } => 2,
            };
        }
        """;

    public const string GeneratedNullableCommandMissing = """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct NullableCommand
        {
            [Variant] public static partial NullableCommand A(bool flag);
            [Variant] public static partial NullableCommand B();
        }
        public static class C
        {
            public static int Read(NullableCommand? command) => command switch
            {
                null => 0,
                { kind: NullableCommand.Kind.A, a: { flag: true } } => 1,
                { kind: NullableCommand.Kind.B, b: var _ } => 2,
            };
        }
        """;

    public const string GeneratedNullableCommandComplete = """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct NullableCommand
        {
            [Variant] public static partial NullableCommand A(bool flag);
            [Variant] public static partial NullableCommand B();
        }
        public static class C
        {
            public static int Read(NullableCommand? command) => command switch
            {
                null => 0,
                { kind: NullableCommand.Kind.A, a: { flag: true } } => 1,
                { kind: NullableCommand.Kind.A, a: { flag: false } } => 2,
                { kind: NullableCommand.Kind.B, b: var _ } => 3,
            };
        }
        """;

    public const string GeneratedNullableCommandMissingNull = """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct NullableCommand
        {
            [Variant] public static partial NullableCommand A(bool flag);
            [Variant] public static partial NullableCommand B();
        }
        public static class C
        {
            public static int Read(NullableCommand? command) => command switch
            {
                { kind: NullableCommand.Kind.A, a: { flag: true } } => 1,
                { kind: NullableCommand.Kind.A, a: { flag: false } } => 2,
                { kind: NullableCommand.Kind.B, b: var _ } => 3,
            };
        }
        """;

    public const string GeneratedRecordCommandMissing = """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public abstract partial record Result
        {
            public sealed partial record A(bool Flag) : Result;
            public sealed partial record B() : Result;
        }
        public static class C
        {
            public static int Read(Result result) => result switch
            {
                Result.A { Flag: true } => 1,
                Result.B => 2,
            };
        }
        """;

    public const string GeneratedRecordCommandComplete = """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public abstract partial record Result
        {
            public sealed partial record A(bool Flag) : Result;
            public sealed partial record B() : Result;
        }
        public static class C
        {
            public static int Read(Result result) => result switch
            {
                Result.A { Flag: true } => 1,
                Result.A { Flag: false } => 2,
                Result.B => 3,
            };
        }
        """;
}
