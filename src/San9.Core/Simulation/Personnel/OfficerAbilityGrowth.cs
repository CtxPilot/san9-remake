using San9.Core.Domain;

namespace San9.Core.Simulation;

internal enum OfficerAbility { Leadership, War, Intelligence, Politics }

internal static class OfficerAbilityGrowth
{
    internal static void TryIncrease(WorldMutation world, int officerId, OfficerAbility ability,
        string stream, List<DomainEvent> events)
    {
        var officer = world.Officers[officerId];
        int current = Value(officer.BaseAbilities, ability);
        if (!world.OfficerGrowth.CanIncrease(current)) return;
        bool belongsToPlayer = officer.OwnerId is int force && world.Simulation.Controls.Any(c =>
            c.ForceId == force && c.Kind == ControllerKind.Player);
        int chance = OfficerGrowthRules.GrowthChance(current, world.Companion.DynamicOfficerAbilityGrowth, belongsToPlayer);
        if (world.NextRandom(stream) % 100 < (uint)chance) Increase(world, officerId, ability, 1, events);
    }

    internal static void Increase(WorldMutation world, int officerId, OfficerAbility ability,
        int amount, List<DomainEvent> events)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var officer = world.Officers[officerId];
        var abilities = officer.BaseAbilities;
        int before = Value(abilities, ability);
        int next = world.OfficerGrowth.Increase(before, amount);
        if (next == before) return;
        var updated = ability switch
        {
            OfficerAbility.Leadership => abilities with { Leadership = next },
            OfficerAbility.War => abilities with { War = next },
            OfficerAbility.Intelligence => abilities with { Intelligence = next },
            OfficerAbility.Politics => abilities with { Politics = next },
            _ => throw new ArgumentOutOfRangeException(nameof(ability))
        };
        world.Replace(officer with { BaseAbilities = updated });
        events.Add(new OfficerAbilityGrownEvent(world.Clock, officerId, ability.ToString().ToLowerInvariant(), before, next)
        { Occurrence = PersonOccurrence.Capture(world, officer) });
    }

    private static int Value(OfficerAbilities abilities, OfficerAbility ability) => ability switch
    {
        OfficerAbility.Leadership => abilities.Leadership,
        OfficerAbility.War => abilities.War,
        OfficerAbility.Intelligence => abilities.Intelligence,
        OfficerAbility.Politics => abilities.Politics,
        _ => throw new ArgumentOutOfRangeException(nameof(ability))
    };
}
