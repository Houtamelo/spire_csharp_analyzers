using System;

namespace Houtamelo.Spire;

/// <summary>
/// Safe integer-to-enum conversion utilities.
/// Use <see cref="TryFrom(long, out TEnum)"/> for non-[Flags] enums
/// and <see cref="TryFromFlags(long, out TEnum)"/> for [Flags] enums.
/// </summary>
public static class SpireEnum<TEnum> where TEnum : struct, Enum
{
    private static readonly TEnum[] AllValues = (TEnum[])Enum.GetValues(typeof(TEnum));
    private static readonly TypeCode UnderlyingTypeCode = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum)));

    /// <summary>
    /// Returns the first named member of <typeparamref name="TEnum"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The enum has no named members.</exception>
    public static TEnum Fallback()
    {
        if (AllValues.Length == 0)
            throw new InvalidOperationException(
                $"Enum type '{typeof(TEnum).Name}' has no named members.");

        return AllValues[0];
    }

    // ── Non-flags: TryFrom ──

    /// <summary>
    /// Attempts to convert a <see cref="long"/> to a named member of <typeparamref name="TEnum"/>.
    /// </summary>
    public static bool TryFrom(long value, out TEnum result)
    {
        // Enum.IsDefined only accepts the enum's underlying type.
        // Convert the long to that type; if it overflows, it's not a valid member.
        object converted;
        try
        {
            converted = ConvertToUnderlyingType(value);
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }

        if (Enum.IsDefined(typeof(TEnum), converted))
        {
            result = (TEnum)Enum.ToObject(typeof(TEnum), value);
            return true;
        }

        result = default;
        return false;
    }

    /// <inheritdoc cref="TryFrom(long, out TEnum)"/>
    public static bool TryFrom(int value, out TEnum result) => TryFrom((long)value, out result);
    /// <inheritdoc cref="TryFrom(long, out TEnum)"/>
    public static bool TryFrom(short value, out TEnum result) => TryFrom((long)value, out result);
    /// <inheritdoc cref="TryFrom(long, out TEnum)"/>
    public static bool TryFrom(sbyte value, out TEnum result) => TryFrom((long)value, out result);
    /// <inheritdoc cref="TryFrom(long, out TEnum)"/>
    public static bool TryFrom(uint value, out TEnum result) => TryFrom((long)value, out result);
    /// <inheritdoc cref="TryFrom(long, out TEnum)"/>
    public static bool TryFrom(ushort value, out TEnum result) => TryFrom((long)value, out result);
    /// <inheritdoc cref="TryFrom(long, out TEnum)"/>
    public static bool TryFrom(byte value, out TEnum result) => TryFrom((long)value, out result);

    /// <summary>
    /// Attempts to convert a <see cref="ulong"/> to a named member of <typeparamref name="TEnum"/>.
    /// </summary>
    public static bool TryFrom(ulong value, out TEnum result)
    {
        if (value <= long.MaxValue)
            return TryFrom((long)value, out result);

        // Value exceeds long.MaxValue — only valid for ulong-backed enums.
        if (UnderlyingTypeCode != TypeCode.UInt64)
        {
            result = default;
            return false;
        }

        if (Enum.IsDefined(typeof(TEnum), value))
        {
            result = (TEnum)Enum.ToObject(typeof(TEnum), value);
            return true;
        }

        result = default;
        return false;
    }

    // ── Non-flags: From ──

    /// <summary>
    /// Converts a <see cref="long"/> to a named member of <typeparamref name="TEnum"/>,
    /// throwing if the value is not defined.
    /// </summary>
    public static TEnum From(long value)
    {
        if (!TryFrom(value, out var result))
            throw new ArgumentOutOfRangeException(
                nameof(value), value,
                $"Value {value} does not map to a valid member of '{typeof(TEnum).Name}'.");
        return result;
    }

    /// <inheritdoc cref="From(long)"/>
    public static TEnum From(int value) => From((long)value);
    /// <inheritdoc cref="From(long)"/>
    public static TEnum From(short value) => From((long)value);
    /// <inheritdoc cref="From(long)"/>
    public static TEnum From(sbyte value) => From((long)value);
    /// <inheritdoc cref="From(long)"/>
    public static TEnum From(uint value) => From((long)value);
    /// <inheritdoc cref="From(long)"/>
    public static TEnum From(ushort value) => From((long)value);
    /// <inheritdoc cref="From(long)"/>
    public static TEnum From(byte value) => From((long)value);

    /// <summary>
    /// Converts a <see cref="ulong"/> to a named member of <typeparamref name="TEnum"/>,
    /// throwing if the value is not defined.
    /// </summary>
    public static TEnum From(ulong value)
    {
        if (!TryFrom(value, out var result))
            throw new ArgumentOutOfRangeException(
                nameof(value), value,
                $"Value {value} does not map to a valid member of '{typeof(TEnum).Name}'.");
        return result;
    }

    // ── Non-flags: FromOrFallback ──

    /// <summary>
    /// Converts a <see cref="long"/> to a named member of <typeparamref name="TEnum"/>,
    /// returning <see cref="Fallback"/> if the value is not defined.
    /// </summary>
    public static TEnum FromOrFallback(long value) => TryFrom(value, out var r) ? r : Fallback();
    /// <inheritdoc cref="FromOrFallback(long)"/>
    public static TEnum FromOrFallback(int value) => FromOrFallback((long)value);
    /// <inheritdoc cref="FromOrFallback(long)"/>
    public static TEnum FromOrFallback(short value) => FromOrFallback((long)value);
    /// <inheritdoc cref="FromOrFallback(long)"/>
    public static TEnum FromOrFallback(sbyte value) => FromOrFallback((long)value);
    /// <inheritdoc cref="FromOrFallback(long)"/>
    public static TEnum FromOrFallback(uint value) => FromOrFallback((long)value);
    /// <inheritdoc cref="FromOrFallback(long)"/>
    public static TEnum FromOrFallback(ushort value) => FromOrFallback((long)value);
    /// <inheritdoc cref="FromOrFallback(long)"/>
    public static TEnum FromOrFallback(byte value) => FromOrFallback((long)value);
    /// <summary>
    /// Converts a <see cref="ulong"/> to a named member of <typeparamref name="TEnum"/>,
    /// returning <see cref="Fallback"/> if the value is not defined.
    /// </summary>
    public static TEnum FromOrFallback(ulong value) => TryFrom(value, out var r) ? r : Fallback();

    // ── Flags: TryFromFlags ──

    /// <summary>
    /// Attempts to convert a <see cref="long"/> to a valid flags combination of <typeparamref name="TEnum"/>.
    /// </summary>
    public static bool TryFromFlags(long value, out TEnum result)
    {
        if (IsValidFlagsCombination(value))
        {
            result = (TEnum)Enum.ToObject(typeof(TEnum), value);
            return true;
        }

        result = default;
        return false;
    }

    /// <inheritdoc cref="TryFromFlags(long, out TEnum)"/>
    public static bool TryFromFlags(int value, out TEnum result) => TryFromFlags((long)value, out result);
    /// <inheritdoc cref="TryFromFlags(long, out TEnum)"/>
    public static bool TryFromFlags(short value, out TEnum result) => TryFromFlags((long)value, out result);
    /// <inheritdoc cref="TryFromFlags(long, out TEnum)"/>
    public static bool TryFromFlags(sbyte value, out TEnum result) => TryFromFlags((long)value, out result);
    /// <inheritdoc cref="TryFromFlags(long, out TEnum)"/>
    public static bool TryFromFlags(uint value, out TEnum result) => TryFromFlags((long)value, out result);
    /// <inheritdoc cref="TryFromFlags(long, out TEnum)"/>
    public static bool TryFromFlags(ushort value, out TEnum result) => TryFromFlags((long)value, out result);
    /// <inheritdoc cref="TryFromFlags(long, out TEnum)"/>
    public static bool TryFromFlags(byte value, out TEnum result) => TryFromFlags((long)value, out result);

    /// <summary>
    /// Attempts to convert a <see cref="ulong"/> to a valid flags combination of <typeparamref name="TEnum"/>.
    /// </summary>
    public static bool TryFromFlags(ulong value, out TEnum result)
    {
        if (IsValidFlagsCombinationUlong(value))
        {
            result = (TEnum)Enum.ToObject(typeof(TEnum), value);
            return true;
        }

        result = default;
        return false;
    }

    // ── Flags: FromFlags ──

    /// <summary>
    /// Converts a <see cref="long"/> to a valid flags combination of <typeparamref name="TEnum"/>,
    /// throwing if not valid.
    /// </summary>
    public static TEnum FromFlags(long value)
    {
        if (!TryFromFlags(value, out var result))
            throw new ArgumentOutOfRangeException(
                nameof(value), value,
                $"Value {value} does not map to a valid flags combination of '{typeof(TEnum).Name}'.");
        return result;
    }

    /// <inheritdoc cref="FromFlags(long)"/>
    public static TEnum FromFlags(int value) => FromFlags((long)value);
    /// <inheritdoc cref="FromFlags(long)"/>
    public static TEnum FromFlags(short value) => FromFlags((long)value);
    /// <inheritdoc cref="FromFlags(long)"/>
    public static TEnum FromFlags(sbyte value) => FromFlags((long)value);
    /// <inheritdoc cref="FromFlags(long)"/>
    public static TEnum FromFlags(uint value) => FromFlags((long)value);
    /// <inheritdoc cref="FromFlags(long)"/>
    public static TEnum FromFlags(ushort value) => FromFlags((long)value);
    /// <inheritdoc cref="FromFlags(long)"/>
    public static TEnum FromFlags(byte value) => FromFlags((long)value);

    /// <summary>
    /// Converts a <see cref="ulong"/> to a valid flags combination of <typeparamref name="TEnum"/>,
    /// throwing if not valid.
    /// </summary>
    public static TEnum FromFlags(ulong value)
    {
        if (!TryFromFlags(value, out var result))
            throw new ArgumentOutOfRangeException(
                nameof(value), value,
                $"Value {value} does not map to a valid flags combination of '{typeof(TEnum).Name}'.");
        return result;
    }

    // ── Flags: FromFlagsOrFallback ──

    /// <summary>
    /// Converts a <see cref="long"/> to a valid flags combination of <typeparamref name="TEnum"/>,
    /// returning <see cref="Fallback"/> if not valid.
    /// </summary>
    public static TEnum FromFlagsOrFallback(long value) => TryFromFlags(value, out var r) ? r : Fallback();
    /// <inheritdoc cref="FromFlagsOrFallback(long)"/>
    public static TEnum FromFlagsOrFallback(int value) => FromFlagsOrFallback((long)value);
    /// <inheritdoc cref="FromFlagsOrFallback(long)"/>
    public static TEnum FromFlagsOrFallback(short value) => FromFlagsOrFallback((long)value);
    /// <inheritdoc cref="FromFlagsOrFallback(long)"/>
    public static TEnum FromFlagsOrFallback(sbyte value) => FromFlagsOrFallback((long)value);
    /// <inheritdoc cref="FromFlagsOrFallback(long)"/>
    public static TEnum FromFlagsOrFallback(uint value) => FromFlagsOrFallback((long)value);
    /// <inheritdoc cref="FromFlagsOrFallback(long)"/>
    public static TEnum FromFlagsOrFallback(ushort value) => FromFlagsOrFallback((long)value);
    /// <inheritdoc cref="FromFlagsOrFallback(long)"/>
    public static TEnum FromFlagsOrFallback(byte value) => FromFlagsOrFallback((long)value);
    /// <summary>
    /// Converts a <see cref="ulong"/> to a valid flags combination of <typeparamref name="TEnum"/>,
    /// returning <see cref="Fallback"/> if not valid.
    /// </summary>
    public static TEnum FromFlagsOrFallback(ulong value) => TryFromFlags(value, out var r) ? r : Fallback();

    // ── Flags validation internals ──

    private static bool IsValidFlagsCombination(long value)
    {
        if (value < 0)
            return Enum.IsDefined(typeof(TEnum), value);

        return IsValidFlagsCombinationUlong((ulong)value);
    }

    private static bool IsValidFlagsCombinationUlong(ulong bits)
    {
        ulong allNamedBits = 0;
        bool hasZeroMember = false;

        foreach (var member in AllValues)
        {
            ulong memberValue = Convert.ToUInt64(member);
            if (memberValue == 0)
                hasZeroMember = true;
            else
                allNamedBits |= memberValue;
        }

        if (bits == 0)
            return hasZeroMember;

        return (bits & ~allNamedBits) == 0;
    }

    // ── Underlying type conversion ──

    /// Converts a long to the enum's underlying type, throwing OverflowException if it doesn't fit.
    private static object ConvertToUnderlyingType(long value)
    {
        switch (UnderlyingTypeCode)
        {
            case TypeCode.SByte:
                return checked((sbyte)value);
            case TypeCode.Byte:
                return checked((byte)value);
            case TypeCode.Int16:
                return checked((short)value);
            case TypeCode.UInt16:
                return checked((ushort)value);
            case TypeCode.Int32:
                return checked((int)value);
            case TypeCode.UInt32:
                return checked((uint)value);
            case TypeCode.Int64:
                return value;
            case TypeCode.UInt64:
                return checked((ulong)value);
            default:
                throw new NotSupportedException(
                    $"Unsupported underlying type code '{UnderlyingTypeCode}' for enum '{typeof(TEnum).Name}'.");
        }
    }
}
