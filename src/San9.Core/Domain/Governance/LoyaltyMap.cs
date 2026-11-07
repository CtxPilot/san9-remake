using System.Collections.Immutable;

namespace San9.Core.Domain;

/// <summary>Immutable loyalty overrides interpreted against a content-supplied baseline.</summary>
internal sealed record LoyaltyMap(int DefaultValue, ImmutableDictionary<int, int> Overrides)
{
    internal static LoyaltyMap Empty { get; } = new(50, ImmutableDictionary<int, int>.Empty);

    internal int Get(int forceId) => Overrides.GetValueOrDefault(forceId, DefaultValue);

    internal LoyaltyMap Set(int forceId, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(forceId);
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 255);
        var overrides = value == DefaultValue ? Overrides.Remove(forceId) : Overrides.SetItem(forceId, value);
        return ReferenceEquals(overrides, Overrides) ? this : this with { Overrides = overrides };
    }

    internal LoyaltyMap Bind(int defaultValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(defaultValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(defaultValue, 255);
        var overrides = Overrides?.Any(p => p.Value == defaultValue) == true
            ? Overrides.RemoveRange(Overrides.Where(p => p.Value == defaultValue).Select(p => p.Key)) : Overrides;
        return defaultValue == DefaultValue && ReferenceEquals(overrides, Overrides)
            ? this : new(defaultValue, overrides!);
    }
}
