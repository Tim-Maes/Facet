using System;

namespace Facet;

/// <summary>
/// Represents an optional value that can be either specified or unspecified.
/// This is useful for PATCH operations where you need to distinguish between
/// a value that was not provided and a value that was explicitly set to null or a specific value.
/// </summary>
/// <typeparam name="T">The type of the optional value.</typeparam>
public readonly struct Optional<T>
{
    private readonly T _value;
    private readonly bool _hasValue;

    /// <summary>
    /// Gets a value indicating whether this optional has a value.
    /// </summary>
    public bool HasValue => _hasValue;

    /// <summary>
    /// Gets the value of this optional.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when HasValue is false.</exception>
    public T Value
    {
        get
        {
            if (!_hasValue)
                throw new InvalidOperationException("Optional does not have a value.");
            return _value;
        }
    }

    /// <summary>
    /// Creates an optional with a value.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    public Optional(T value)
    {
        _value = value;
        _hasValue = true;
    }

    /// <summary>
    /// Gets the value if present, otherwise returns the default value for the type.
    /// </summary>
    /// <param name="defaultValue">The default value to return if no value is present.</param>
    /// <returns>The value if present, otherwise the default value.</returns>
    public T GetValueOrDefault(T defaultValue = default!)
    {
        return _hasValue ? _value : defaultValue;
    }

    /// <summary>
    /// Implicitly converts a value to an Optional.
    /// </summary>
    public static implicit operator Optional<T>(T value) => new(value);

    /// <summary>
    /// Converts the optional to its string representation.
    /// </summary>
    public override string ToString()
    {
        return _hasValue ? _value?.ToString() ?? "null" : "unspecified";
    }

    /// <summary>
    /// Determines whether this optional equals another optional.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Optional<T> other)
            return false;

        if (!_hasValue && !other._hasValue)
            return true;

        if (_hasValue != other._hasValue)
            return false;

        return Equals(_value, other._value);
    }

    /// <summary>
    /// Gets the hash code for this optional.
    /// </summary>
    public override int GetHashCode()
    {
        if (!_hasValue)
            return 0;

        return _value?.GetHashCode() ?? 1;
    }

    /// <summary>
    /// Determines whether two optionals are equal.
    /// </summary>
    public static bool operator ==(Optional<T> left, Optional<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two optionals are not equal.
    /// </summary>
    public static bool operator !=(Optional<T> left, Optional<T> right) => !left.Equals(right);
}
