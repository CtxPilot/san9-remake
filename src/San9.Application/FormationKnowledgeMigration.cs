using San9.Core.Domain;

namespace San9.Application;

internal static class FormationKnowledgeMigration
{
    internal static OfficerState[] Initialize(IEnumerable<OfficerState> officers, IReadOnlyCollection<ForceState> forces,
        IReadOnlyCollection<UnitState> units, MilitaryRulebook rules)
    {
        var byForce = forces.ToDictionary(f => f.Id);
        var activeFormations = units.Where(u => u.CommanderId is not null && u.FormationId is not null)
            .ToDictionary(u => u.CommanderId!.Value, u => u.FormationId!.Value);
        return officers.Select(person => FormationKnowledge.InitializeLegacy(person,
            person.OwnerId is int owner && byForce.TryGetValue(owner, out var force) ? force : null,
            rules, activeFormations.TryGetValue(person.Id, out int formation) ? formation : null)).ToArray();
    }

    internal static SessionSnapshot Initialize(SessionSnapshot snapshot, MilitaryRulebook? rules)
    {
        if (!snapshot.World.Simulation.NewGame.MixedTroopsEnabled || rules is null ||
            snapshot.World.Officers.All(o => o.FormationProficiencies is not null)) return snapshot;
        return snapshot with { World = snapshot.World with
        {
            Officers = Initialize(snapshot.World.Officers, snapshot.World.Forces, snapshot.World.Units, rules)
        } };
    }
}
