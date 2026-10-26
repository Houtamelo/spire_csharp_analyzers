using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Houtamelo.Spire.PatternAnalysis.Domains.Numeric;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.PatternAnalysis.Domains;

/// Domain wrapping exact integral ranges or the existing floating interval set.
internal sealed class NumericDomain : IValueDomain
{
    readonly bool _isIntegral;
    readonly IntervalSet _intervals;
    readonly IntegralIntervalSet _integralIntervals;
    readonly double _universeMin;
    readonly double _universeMax;
    readonly BigInteger _integralUniverseMin;
    readonly BigInteger _integralUniverseMax;

    public ITypeSymbol Type { get; }

    /// True when this domain represents one of the eight fixed-width integral types.
    public bool IsIntegral => _isIntegral;

    /// Floating interval state. Accessing this property for an integral domain
    /// is a representation error; use IntegralIntervals instead.
    public IntervalSet Intervals => _isIntegral
        ? throw new InvalidOperationException("Integral NumericDomain has no floating intervals")
        : _intervals;

    /// Exact interval state for integral domains.
    public IntegralIntervalSet IntegralIntervals => !_isIntegral
        ? throw new InvalidOperationException("Floating NumericDomain has no integral intervals")
        : _integralIntervals;

    /// Constructs a floating numeric domain. Fixed-width integral domains must
    /// be created through FromIntegralIntervals so no endpoint can pass through
    /// double representation.
    public NumericDomain(
        ITypeSymbol type,
        IntervalSet intervals,
        double universeMin,
        double universeMax)
    {
        if (IsIntegralType(type))
        {
            throw new ArgumentException(
                "Integral NumericDomain values must use FromIntegralIntervals",
                nameof(type));
        }

        Type = type;
        _isIntegral = false;
        _intervals = intervals;
        _universeMin = universeMin;
        _universeMax = universeMax;
        _integralIntervals = IntegralIntervalSet.Empty;
        _integralUniverseMin = BigInteger.Zero;
        _integralUniverseMax = BigInteger.Zero;
    }

    NumericDomain(
        ITypeSymbol type,
        IntegralIntervalSet intervals,
        BigInteger universeMin,
        BigInteger universeMax)
    {
        if (!IsIntegralType(type))
        {
            throw new ArgumentException(
                "Floating NumericDomain values must use the IntervalSet constructor",
                nameof(type));
        }

        Type = type;
        _isIntegral = true;
        _integralIntervals = intervals;
        _integralUniverseMin = universeMin;
        _integralUniverseMax = universeMax;
        _intervals = IntervalSet.Empty;
        _universeMin = 0;
        _universeMax = 0;
    }

    /// Construct an exact integral domain and clamp it to the type universe.
    internal static NumericDomain FromIntegralIntervals(
        ITypeSymbol type,
        IntegralIntervalSet intervals)
    {
        var (min, max) = GetIntegralBounds(type);
        var universe = IntegralIntervalSet.Universe(min, max);
        return new NumericDomain(type, intervals.Intersect(universe), min, max);
    }

    /// Create a universe domain covering all values of the given numeric type.
    public static NumericDomain Universe(ITypeSymbol type)
    {
        if (IsIntegralType(type))
        {
            var (min, max) = GetIntegralBounds(type);
            return FromIntegralIntervals(type, IntegralIntervalSet.Universe(min, max));
        }

        var (floatingMin, floatingMax) = GetBounds(type);
        return new NumericDomain(
            type,
            IntervalSet.Universe(floatingMin, floatingMax),
            floatingMin,
            floatingMax);
    }

    public bool IsEmpty => _isIntegral ? _integralIntervals.IsEmpty : _intervals.IsEmpty;

    public bool IsUniverse => _isIntegral
        ? _integralIntervals.CoversAll(_integralUniverseMin, _integralUniverseMax)
        : _intervals.CoversAll(_universeMin, _universeMax);

    public IValueDomain Subtract(IValueDomain other)
    {
        if (other is not NumericDomain otherNumeric)
            throw new ArgumentException($"Cannot subtract {other.GetType().Name} from NumericDomain");

        if (_isIntegral)
        {
            if (!otherNumeric._isIntegral)
                throw new ArgumentException("Cannot subtract floating NumericDomain from integral NumericDomain");

            return FromIntegralIntervals(
                Type,
                _integralIntervals.Subtract(otherNumeric._integralIntervals));
        }

        if (otherNumeric._isIntegral)
            throw new ArgumentException("Cannot subtract integral NumericDomain from floating NumericDomain");

        return new NumericDomain(
            Type,
            _intervals.Subtract(otherNumeric._intervals),
            _universeMin,
            _universeMax);
    }

    public IValueDomain Intersect(IValueDomain other)
    {
        if (other is not NumericDomain otherNumeric)
            throw new ArgumentException($"Cannot intersect {other.GetType().Name} with NumericDomain");

        if (_isIntegral)
        {
            if (!otherNumeric._isIntegral)
                throw new ArgumentException("Cannot intersect floating NumericDomain with integral NumericDomain");

            return FromIntegralIntervals(
                Type,
                _integralIntervals.Intersect(otherNumeric._integralIntervals));
        }

        if (otherNumeric._isIntegral)
            throw new ArgumentException("Cannot intersect integral NumericDomain with floating NumericDomain");

        return new NumericDomain(
            Type,
            _intervals.Intersect(otherNumeric._intervals),
            _universeMin,
            _universeMax);
    }

    public IValueDomain Complement()
    {
        if (_isIntegral)
        {
            return FromIntegralIntervals(
                Type,
                _integralIntervals.Complement(_integralUniverseMin, _integralUniverseMax));
        }

        return new NumericDomain(
            Type,
            _intervals.Complement(_universeMin, _universeMax),
            _universeMin,
            _universeMax);
    }

    public ImmutableArray<IValueDomain> Split()
    {
        if (IsEmpty)
            return ImmutableArray<IValueDomain>.Empty;

        if (_isIntegral)
        {
            var boundaryBuilder = ImmutableArray.CreateBuilder<BigInteger>();
            foreach (var interval in _integralIntervals.Intervals)
            {
                if (interval.Lo > _integralUniverseMin)
                    boundaryBuilder.Add(interval.Lo);
                if (interval.Hi < _integralUniverseMax)
                    boundaryBuilder.Add(interval.Hi);
            }

            var boundaries = DistinctSorted(boundaryBuilder.ToImmutable());
            if (boundaries.IsDefaultOrEmpty)
                return ImmutableArray.Create<IValueDomain>(this);

            var partitions = _integralIntervals.SplitByBoundaries(
                boundaries,
                _integralUniverseMin,
                _integralUniverseMax);
            var result = ImmutableArray.CreateBuilder<IValueDomain>(partitions.Length);
            foreach (var partition in partitions)
            {
                if (!partition.IsEmpty)
                    result.Add(FromIntegralIntervals(Type, partition));
            }

            return result.ToImmutable();
        }

        var floatingBoundaryBuilder = ImmutableArray.CreateBuilder<double>();
        foreach (var interval in _intervals.Intervals)
        {
            if (interval.Lo > _universeMin)
                floatingBoundaryBuilder.Add(interval.Lo);
            if (interval.Hi < _universeMax)
                floatingBoundaryBuilder.Add(interval.Hi);
        }

        var floatingBoundaries = DistinctSorted(floatingBoundaryBuilder.ToImmutable());
        if (floatingBoundaries.IsDefaultOrEmpty)
            return ImmutableArray.Create<IValueDomain>(this);

        var floatingPartitions = _intervals.SplitByBoundaries(floatingBoundaries);
        var floatingResult = ImmutableArray.CreateBuilder<IValueDomain>(floatingPartitions.Length);
        foreach (var partition in floatingPartitions)
        {
            if (!partition.IsEmpty)
            {
                floatingResult.Add(new NumericDomain(
                    Type,
                    partition,
                    _universeMin,
                    _universeMax));
            }
        }

        return floatingResult.ToImmutable();
    }

    /// Create an exact or floating singleton using the source domain's own
    /// universe. The source value is deliberately kept typed until this
    /// method selects the representation.
    internal bool TryCreatePoint(object value, out NumericDomain point)
    {
        if (_isIntegral)
        {
            if (!TryGetIntegralValue(value, out var integralValue))
            {
                point = EmptyLike();
                return false;
            }

            var singleton = IntegralIntervalSet.Single(
                new IntegralInterval(
                    integralValue,
                    integralValue,
                    loInclusive: true,
                    hiInclusive: true));
            point = FromIntegralIntervals(Type, singleton);
            return true;
        }

        if (!TryConvertDouble(value, out var floatingValue))
        {
            point = EmptyLike();
            return false;
        }

        point = new NumericDomain(
            Type,
            IntervalSet.Single(new Interval(floatingValue, floatingValue, loInclusive: true, hiInclusive: true)),
            _universeMin,
            _universeMax);
        return true;
    }

    /// Create a relational range using exact endpoint arithmetic for integral
    /// domains and the existing double path for floating domains.
    internal bool TryCreateRange(
        object value,
        BinaryOperatorKind operatorKind,
        out NumericDomain range)
    {
        if (_isIntegral)
        {
            if (!TryGetIntegralValue(value, out var bound))
            {
                range = EmptyLike();
                return false;
            }

            IntegralInterval interval;
            switch (operatorKind)
            {
                case BinaryOperatorKind.LessThan:
                    interval = new IntegralInterval(
                        _integralUniverseMin,
                        bound,
                        loInclusive: true,
                        hiInclusive: false);
                    break;
                case BinaryOperatorKind.LessThanOrEqual:
                    interval = new IntegralInterval(
                        _integralUniverseMin,
                        bound,
                        loInclusive: true,
                        hiInclusive: true);
                    break;
                case BinaryOperatorKind.GreaterThan:
                    interval = new IntegralInterval(
                        bound,
                        _integralUniverseMax,
                        loInclusive: false,
                        hiInclusive: true);
                    break;
                case BinaryOperatorKind.GreaterThanOrEqual:
                    interval = new IntegralInterval(
                        bound,
                        _integralUniverseMax,
                        loInclusive: true,
                        hiInclusive: true);
                    break;
                default:
                    range = EmptyLike();
                    return false;
            }

            range = FromIntegralIntervals(Type, IntegralIntervalSet.Single(interval));
            return true;
        }

        if (!TryConvertDouble(value, out var boundAsDouble))
        {
            range = EmptyLike();
            return false;
        }

        Interval floatingInterval;
        switch (operatorKind)
        {
            case BinaryOperatorKind.LessThan:
                floatingInterval = new Interval(_universeMin, boundAsDouble, true, false);
                break;
            case BinaryOperatorKind.LessThanOrEqual:
                floatingInterval = new Interval(_universeMin, boundAsDouble, true, true);
                break;
            case BinaryOperatorKind.GreaterThan:
                floatingInterval = new Interval(boundAsDouble, _universeMax, false, true);
                break;
            case BinaryOperatorKind.GreaterThanOrEqual:
                floatingInterval = new Interval(boundAsDouble, _universeMax, true, true);
                break;
            default:
                range = EmptyLike();
                return false;
        }

        range = new NumericDomain(
            Type,
            IntervalSet.Single(floatingInterval),
            _universeMin,
            _universeMax);
        return true;
    }

    /// Empty domain with the same exactness and universe as this domain.
    internal NumericDomain EmptyLike()
    {
        return _isIntegral
            ? FromIntegralIntervals(Type, IntegralIntervalSet.Empty)
            : new NumericDomain(Type, IntervalSet.Empty, _universeMin, _universeMax);
    }

    internal static bool IsIntegralConstant(object value) =>
        value is sbyte || value is byte || value is short || value is ushort ||
        value is int || value is uint || value is long || value is ulong ||
        value is char || value is BigInteger;

    static bool TryGetIntegralValue(object value, out BigInteger result)
    {
        switch (value)
        {
            case BigInteger bigInteger:
                result = bigInteger;
                return true;
            case sbyte signedByte:
                result = new BigInteger(signedByte);
                return true;
            case byte unsignedByte:
                result = new BigInteger(unsignedByte);
                return true;
            case short signedShort:
                result = new BigInteger(signedShort);
                return true;
            case ushort unsignedShort:
                result = new BigInteger(unsignedShort);
                return true;
            case int signedInt:
                result = new BigInteger(signedInt);
                return true;
            case uint unsignedInt:
                result = new BigInteger(unsignedInt);
                return true;
            case long signedLong:
                result = new BigInteger(signedLong);
                return true;
            case ulong unsignedLong:
                result = new BigInteger(unsignedLong);
                return true;
            case char character:
                result = new BigInteger(character);
                return true;
            default:
                result = BigInteger.Zero;
                return false;
        }
    }

    static bool TryConvertDouble(object value, out double result)
    {
        if (value is IConvertible convertible)
        {
            try
            {
                result = convertible.ToDouble(CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                // Fall through to the conservative failure path.
            }
        }

        result = 0;
        return false;
    }

    static ImmutableArray<T> DistinctSorted<T>(ImmutableArray<T> values)
        where T : IComparable<T>
    {
        if (values.IsDefaultOrEmpty)
            return ImmutableArray<T>.Empty;

        return values.Distinct().OrderBy(value => value).ToImmutableArray();
    }

    static (double min, double max) GetBounds(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Single => (float.MinValue, float.MaxValue),
            SpecialType.System_Double => (double.MinValue, double.MaxValue),
            SpecialType.System_Decimal => ((double)decimal.MinValue, (double)decimal.MaxValue),
            _ => throw new ArgumentException($"Unsupported floating numeric type: {type.ToDisplayString()}")
        };
    }

    static (BigInteger min, BigInteger max) GetIntegralBounds(ITypeSymbol type)
    {
        return type.SpecialType switch
        {
            SpecialType.System_Byte => (new BigInteger(byte.MinValue), new BigInteger(byte.MaxValue)),
            SpecialType.System_SByte => (new BigInteger(sbyte.MinValue), new BigInteger(sbyte.MaxValue)),
            SpecialType.System_Int16 => (new BigInteger(short.MinValue), new BigInteger(short.MaxValue)),
            SpecialType.System_UInt16 => (new BigInteger(ushort.MinValue), new BigInteger(ushort.MaxValue)),
            SpecialType.System_Int32 => (new BigInteger(int.MinValue), new BigInteger(int.MaxValue)),
            SpecialType.System_UInt32 => (new BigInteger(uint.MinValue), new BigInteger(uint.MaxValue)),
            SpecialType.System_Int64 => (new BigInteger(long.MinValue), new BigInteger(long.MaxValue)),
            SpecialType.System_UInt64 => (new BigInteger(ulong.MinValue), new BigInteger(ulong.MaxValue)),
            _ => throw new ArgumentException($"Unsupported integral type: {type.ToDisplayString()}")
        };
    }

    static bool IsIntegralType(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Byte => true,
        SpecialType.System_SByte => true,
        SpecialType.System_Int16 => true,
        SpecialType.System_UInt16 => true,
        SpecialType.System_Int32 => true,
        SpecialType.System_UInt32 => true,
        SpecialType.System_Int64 => true,
        SpecialType.System_UInt64 => true,
        _ => false,
    };
}
