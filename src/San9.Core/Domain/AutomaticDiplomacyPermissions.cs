using System.Text.Json.Serialization;

namespace San9.Core.Domain;

public enum AutomaticDiplomacyKind { Gift, RequestAttack, ReturnPrisoner, Surrender }

/// <summary>Host policy for future automatic diplomacy; public commands and existing missions retain their rules.</summary>
public sealed record AutomaticDiplomacyPermissions
{
    public static AutomaticDiplomacyPermissions All { get; } = new();

    [JsonRequired] public bool Gift { get; init; } = true;
    [JsonRequired] public bool RequestAttack { get; init; } = true;
    [JsonRequired] public bool ReturnPrisoner { get; init; } = true;
    [JsonRequired] public bool Surrender { get; init; } = true;

    public bool Allows(AutomaticDiplomacyKind kind) => kind switch
    {
        AutomaticDiplomacyKind.Gift => Gift,
        AutomaticDiplomacyKind.RequestAttack => RequestAttack,
        AutomaticDiplomacyKind.ReturnPrisoner => ReturnPrisoner,
        AutomaticDiplomacyKind.Surrender => Surrender,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public AutomaticDiplomacyPermissions WithAllowed(AutomaticDiplomacyKind kind, bool allowed) => kind switch
    {
        AutomaticDiplomacyKind.Gift => this with { Gift = allowed },
        AutomaticDiplomacyKind.RequestAttack => this with { RequestAttack = allowed },
        AutomaticDiplomacyKind.ReturnPrisoner => this with { ReturnPrisoner = allowed },
        AutomaticDiplomacyKind.Surrender => this with { Surrender = allowed },
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
