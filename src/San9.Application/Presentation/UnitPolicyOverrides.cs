using San9.Core.Domain;

namespace San9.Application.Presentation;

public enum UnitPolicyField { AttackApproachingEnemies, AutonomousRetreat, Pursue, AttackAfterAction }

/// <summary>Nullable fields mean preserve each unit's existing value.</summary>
public sealed record UnitPolicyOverrides(bool? AttackApproachingEnemies = null, bool? AutonomousRetreat = null,
    bool? Pursue = null, bool? AttackAfterAction = null)
{
    public bool? Get(UnitPolicyField field) => field switch
    {
        UnitPolicyField.AttackApproachingEnemies => AttackApproachingEnemies,
        UnitPolicyField.AutonomousRetreat => AutonomousRetreat,
        UnitPolicyField.Pursue => Pursue,
        UnitPolicyField.AttackAfterAction => AttackAfterAction,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
    public UnitPolicyOverrides With(UnitPolicyField field, bool? value) => field switch
    {
        UnitPolicyField.AttackApproachingEnemies => this with { AttackApproachingEnemies = value },
        UnitPolicyField.AutonomousRetreat => this with { AutonomousRetreat = value },
        UnitPolicyField.Pursue => this with { Pursue = value },
        UnitPolicyField.AttackAfterAction => this with { AttackAfterAction = value },
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
    public UnitPolicies Apply(UnitPolicies original) => original with
    {
        AttackApproachingEnemies = AttackApproachingEnemies ?? original.AttackApproachingEnemies,
        AutonomousRetreat = AutonomousRetreat ?? original.AutonomousRetreat,
        Pursue = Pursue ?? original.Pursue,
        AttackAfterAction = AttackAfterAction ?? original.AttackAfterAction
    };
}
