namespace San9.Core.Domain;

/// <summary>Demolition needs another valid facility in the same directly owning corps, independently of its geographic city.</summary>
public static class FacilityDemolitionRules
{
    public static bool IsOtherDirectFacility(SettlementState source, SettlementState candidate) =>
        candidate.Id != source.Id && candidate.OwnerId == source.OwnerId && candidate.CorpsId == source.CorpsId &&
        !candidate.IsDormant && !candidate.IsDestroyed;
}
