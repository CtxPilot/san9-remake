using System.Collections.Immutable;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application;

/// <summary>One scenario-owned compilation of authored definitions, shared by simulation and projections.</summary>
internal sealed class ScenarioRules
{
    private readonly SimulationRules _simulation;
    internal EconomyRulebook Economy { get; }
    internal PersonnelRulebook Personnel { get; }
    internal TreasureRulebook Treasures { get; }
    internal EquipmentRulebook Equipment { get; }
    internal GovernanceRulebook Governance { get; }
    internal MilitaryRulebook? Military { get; }
    internal MilitaryCityNetwork MilitaryCities { get; }
    internal PoliticalRulebook Politics { get; }
    internal OfficerAbilityRules Abilities { get; }

    internal ScenarioRules(ScenarioInitialState definition)
    {
        Economy = new EconomyRulebook(definition.Information.Offices.Select(o => new OfficeSalary(o.Id, o.Salary)).ToImmutableArray(),
            definition.DomesticPairs, definition.ImperialSealId)
        { FoodMarket = definition.FoodMarket, ImperialCourt = definition.ImperialCourt, RegionCityIds = definition.RegionCityIds.ToImmutableDictionary() };
        Personnel = new PersonnelRulebook(definition.Settlements.Select(s => new PersonPlace(s.Id, s.Position)).Concat(definition.UnbuiltPlaces), definition.RegionCityIds, definition.CityConnections, definition.CityProvinceIds, definition.TacticCourses, definition.SearchEvents);
        var equipmentDefinitions = definition.Information.Equipment;
        // Older upgraded packages can contain only converted treasures. Supply the same
        // recipes as an empty legacy catalog, while preserving explicitly authored catalogs.
        if (equipmentDefinitions.All(item => item.LegacyTreasureId is not null))
            equipmentDefinitions = equipmentDefinitions.AddRange(EquipmentPreset.Basic());
        Equipment = new EquipmentRulebook(LegacyEquipmentMigration.Convert(definition.Information.Treasures,
            definition.Information.TreasureStates, equipmentDefinitions).Definitions);
        Treasures = new TreasureRulebook(definition.Information.Treasures.Select(t => t.ToRule()), Equipment);
        Governance = new GovernanceRulebook(definition.Information.Offices.Select(o => new GovernorOfficeRule(o.Id, o.CommandCapacity, o.GovernorPriority)
            { AbilityBonuses = o.AbilityBonuses, Salary = o.Salary, RequiredRankId = o.RequiredRankId }),
            definition.Information.Ranks.ToDictionary(r => r.Id, r => r.Precedence));
        if (definition.Military is { } militaryDefinition)
        {
            if (Equipment.Items.Values.Any(e => e.ResearchProjectId == EquipmentPreset.ResearchId) &&
                !militaryDefinition.ResearchProjects.Any(p => p.Id == EquipmentPreset.ResearchId))
                militaryDefinition = militaryDefinition with { ResearchProjects = militaryDefinition.ResearchProjects.Add(EquipmentPreset.Research()) };
            Military = new MilitaryRulebook(militaryDefinition);
            if (Equipment.Items.Values.Any(e => e.ResearchProjectId is string id && !Military.ResearchProjects.ContainsKey(id)))
                throw new InvalidDataException("Unknown equipment research project.");
        }
        MilitaryCities = new(definition.MilitaryCityConnections);
        Politics = new PoliticalRulebook(definition.PoliticalEvents,
            definition.Information.Ranks.Select(r => r.ToRule()),
            definition.Information.CountryTitles.Select(t => t.Rule), definition.ProvinceCountryTitleIds);
        Abilities = new(Treasures, Governance, definition.OfficerGrowth, Equipment);
        _simulation = new()
        {
            Economy = Economy, Personnel = Personnel, Treasures = Treasures, Equipment = Equipment, Governance = Governance, OfficerGrowth = definition.OfficerGrowth,
            Military = Military, Terminal = definition.Terminal, Politics = Politics,
            QiaoMarriage = definition.QiaoMarriage, SunMarriage = definition.SunMarriage,
            HanzhongKing = definition.HanzhongKing, FiveTigers = definition.FiveTigers,
            ExpeditionMemorial = definition.ExpeditionMemorial, ThreeVisits = definition.ThreeVisits,
            ChainedPlot = definition.ChainedPlot, RedCliffs = definition.RedCliffs,
            ShanyangGong = definition.ShanyangGong, Guandu = definition.Guandu
        };
    }

    internal SimulationEngine CreateEngine(WorldState world) => new(world, _simulation);
}
