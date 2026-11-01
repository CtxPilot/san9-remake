using System.Text.Json.Serialization;

namespace San9.Core.Domain;

public enum AutomaticStrategyKind { FalseReport, Disorder, Inspire, Reinforce, Alienate, Burn, Seize, Gossip }

/// <summary>Host policy for future automatic missions; it does not alter public command eligibility.</summary>
public sealed record AutomaticStrategyPermissions
{
    public static AutomaticStrategyPermissions All { get; } = new();

    [JsonRequired] public bool FalseReport { get; init; } = true;
    [JsonRequired] public bool Disorder { get; init; } = true;
    [JsonRequired] public bool Inspire { get; init; } = true;
    [JsonRequired] public bool Reinforce { get; init; } = true;
    [JsonRequired] public bool Alienate { get; init; } = true;
    [JsonRequired] public bool Burn { get; init; } = true;
    [JsonRequired] public bool Seize { get; init; } = true;
    [JsonRequired] public bool Gossip { get; init; } = true;

    public bool Allows(AutomaticStrategyKind kind) => kind switch
    {
        AutomaticStrategyKind.FalseReport => FalseReport,
        AutomaticStrategyKind.Disorder => Disorder,
        AutomaticStrategyKind.Inspire => Inspire,
        AutomaticStrategyKind.Reinforce => Reinforce,
        AutomaticStrategyKind.Alienate => Alienate,
        AutomaticStrategyKind.Burn => Burn,
        AutomaticStrategyKind.Seize => Seize,
        AutomaticStrategyKind.Gossip => Gossip,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public AutomaticStrategyPermissions WithAllowed(AutomaticStrategyKind kind, bool allowed) => kind switch
    {
        AutomaticStrategyKind.FalseReport => this with { FalseReport = allowed },
        AutomaticStrategyKind.Disorder => this with { Disorder = allowed },
        AutomaticStrategyKind.Inspire => this with { Inspire = allowed },
        AutomaticStrategyKind.Reinforce => this with { Reinforce = allowed },
        AutomaticStrategyKind.Alienate => this with { Alienate = allowed },
        AutomaticStrategyKind.Burn => this with { Burn = allowed },
        AutomaticStrategyKind.Seize => this with { Seize = allowed },
        AutomaticStrategyKind.Gossip => this with { Gossip = allowed },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
