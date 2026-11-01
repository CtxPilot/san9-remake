using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace San9.Content.Authoring;

public sealed record OfficerPlacement(Guid OfficerId, int CityId, bool JoinForce);
public sealed record CreatedForce(Guid RulerId, int CityId);

/// <summary>Transient opening draft. Consumed at startup; never retained in a session or save.</summary>
public sealed record NewGameSetup
{
    [JsonRequired] public CreatedOfficerLibrary Library { get; init; } = CreatedOfficerLibrary.Empty;
    [JsonRequired] public ImmutableArray<OfficerPlacement> Placements { get; init; } = [];
    [JsonRequired] public ImmutableArray<CreatedForce> Forces { get; init; } = [];
    public static NewGameSetup Empty { get; } = new();
    public const int MaximumCreatedForces = 8;

    public void Validate()
    {
        if (Library is null || Placements.IsDefault || Forces.IsDefault ||
            Placements.Any(p => p is null || p.CityId < 0) || Forces.Any(f => f is null || f.CityId < 0) ||
            Placements.Select(p => p.OfficerId).Distinct().Count() != Placements.Length ||
            Forces.Length > MaximumCreatedForces || Forces.Select(f => f.RulerId).Distinct().Count() != Forces.Length ||
            Forces.Select(f => f.CityId).Distinct().Count() != Forces.Length)
            throw new InvalidDataException("开局部署资料无效。");
        Library.Validate();
        var ids = Library.Officers.Select(p => p.Id).ToHashSet();
        if (Placements.Any(p => !ids.Contains(p.OfficerId)) || Forces.Any(f => !ids.Contains(f.RulerId) ||
            Placements.Any(p => p.OfficerId == f.RulerId)))
            throw new InvalidDataException("新势力君主与部署人物重复，或人物不在本次人物库中。");
    }
}
