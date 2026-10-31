using System.Collections.Immutable;

namespace San9.Core.Domain;

/// <summary>Authored relationships, independent of affinity and live parent references.</summary>
public sealed record TacticLinkPair(int FirstId, int SecondId, int Bonus, bool SuppressesSiblingBonus);

public sealed record TacticLinkRules(bool RelationshipsEnabled, int KinshipBonus, ImmutableArray<TacticLinkPair> Pairs)
{
    public static TacticLinkRules Empty { get; } = new(false, 0, []);

    internal void Validate()
    {
        if (KinshipBonus is < 0 or > 100 || Pairs.IsDefault ||
            Pairs.Any(p => p.FirstId < 0 || p.SecondId <= p.FirstId || p.Bonus is < 0 or > 100) ||
            Pairs.Select(p => (p.FirstId, p.SecondId)).Distinct().Count() != Pairs.Length)
            throw new ArgumentException("Invalid tactic link relationships.");
    }

    public int Chance(OfficerState source, OfficerState partner, FormationTactics formation, bool historical = true) => Chance(source, partner, formation.LinkMultiplier, historical);

    public int Chance(OfficerState source, OfficerState partner, int multiplier, bool historical = true) =>
        RelationshipBonus(source, partner, historical) + partner.TacticLinkBonus + AffinityBase(source, partner) * multiplier / 2;

    // Cross-category primary selection uses relationship and affinity, without formation or responder-only link bonuses.
    public int ChainChance(OfficerState source, OfficerState candidate, bool historical = true) =>
        RelationshipBonus(source, candidate, historical) + AffinityBase(source, candidate);

    private int RelationshipBonus(OfficerState source, OfficerState partner, bool historical)
    {
        if (!RelationshipsEnabled || !historical) return 0;
        int first = Math.Min(source.Id, partner.Id), second = Math.Max(source.Id, partner.Id);
        var pair = Pairs.FirstOrDefault(p => p.FirstId == first && p.SecondId == second);
        if (source.ParentId is int parent && parent == partner.ParentId && pair?.SuppressesSiblingBonus != true ||
            source.ParentId == partner.Id || partner.ParentId == source.Id) return KinshipBonus;
        return pair?.Bonus ?? 0;
    }

    private static int AffinityBase(OfficerState source, OfficerState partner) =>
        25 + Math.Max(0, 5 * (5 - PersonnelRulebook.AffinityDistance(source, partner)));
}
