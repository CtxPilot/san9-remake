using San9.Content.Text;
using San9.Core.Domain;

namespace San9.Application.Presentation;

/// <summary>Projects retained equipment and its formation compatibility, independently of automatic tactics.</summary>
internal static class UnitTacticDisplay
{
    internal static CatalogCell Read(StrategySnapshot view, MilitaryRulebook? rules, TextCatalog texts,
        UnitState unit, OfficerState officer, int? viewerForceId)
    {
        int? equipped;
        if (unit.OwnerId != viewerForceId)
        {
            var observation = view.ObserveMilitary(viewerForceId ?? -1);
            var equipment = observation?.Units.FirstOrDefault(u => u.Id == unit.Id)?.Equipment.FirstOrDefault(e => e.OfficerId == officer.Id)?.Tactic;
            if (equipment is not { IsKnown: true }) return new("＊＊", IsUnavailable: true);
            equipped = equipment.Value.Value;
            var physical = observation!.Forces.Find(new(unit.Id, false));
            if (physical is null) return CatalogCell.Unavailable;
            unit = new(unit.Id, physical.OwnerId!.Value, physical.Area.Position, physical.Soldiers)
            {
                FormationId = physical.FormationId, NavalFormationId = physical.NavalFormationId,
                Condition = physical.Condition, IsPersonnel = physical.Kind == AI.Military.MilitaryPresenceKind.Personnel
            };
        }
        else equipped = officer.EquippedTacticId;
        if (rules?.TacticFormation(unit) is not { } formation)
            return CatalogCell.Unavailable;
        if (equipped is not int tacticId) return new("");
        var tactic = view.Information.Tactics.First(t => t.Id == tacticId);
        return new(texts.Resolve(tactic.NameKey),
            IsUnavailable: !rules.Tactics[tacticId].CompatibleFormations.Contains(formation.Id));
    }
}
