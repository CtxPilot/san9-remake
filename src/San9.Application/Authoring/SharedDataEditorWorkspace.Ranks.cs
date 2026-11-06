using San9.Content.Packages;
using San9.Content.Scenarios;

namespace San9.Application.Authoring;

public sealed partial class SharedDataEditorWorkspace
{
    private readonly Dictionary<int, RankDefinition> _rankBaselines;
    private Dictionary<int, int> _rankOrderBaseline;

    public RankDefinition GetRank(int id) => Definitions.Information.Ranks.Single(r => r.Id == id);
    public bool IsRankChanged(int id) => !ContentComparison.Equal(GetRank(id), _saved.Information.Ranks.SingleOrDefault(r => r.Id == id));

    public void SetRank(RankDefinition rank)
    {
        ContentStructure.Validate(rank);
        var ranks = Definitions.Information.Ranks.ToArray();
        int index = Array.FindIndex(ranks, r => r.Id == rank.Id);
        if (index < 0) throw new InvalidDataException("只能修改现有爵位，不能改变爵位身份。");
        if (rank.IsUnranked != _rankBaselines[rank.Id].IsUnranked)
            throw new InvalidDataException("无爵位身份固定，不能通过编辑转移。");
        ranks[index] = rank;
        Definitions = Definitions with { Information = Definitions.Information with { Ranks = ranks } };
    }

    public IReadOnlyList<RankDefinition> OrderedRanks => OrderRanks(Ranks).ToArray();
    private static IOrderedEnumerable<RankDefinition> OrderRanks(IEnumerable<RankDefinition> ranks) =>
        ranks.OrderBy(r => r.IsUnranked).ThenByDescending(r => r.Precedence).ThenBy(r => r.Id);

    private static void ValidateRankOrder(IReadOnlyCollection<RankDefinition> ranks)
    {
        if (ranks.Count(r => r.IsUnranked) != 1 || ranks.Count(r => !r.IsUnranked) < 2)
            throw new InvalidDataException("爵位目录须保留唯一的无爵位和至少两个正式爵位。");
        var initial = ranks.Single(r => r.IsUnranked);
        if (ranks.Select(r => r.Precedence).Distinct().Count() != ranks.Count ||
            ranks.Any(r => !r.IsUnranked && r.Precedence <= initial.Precedence))
            throw new InvalidDataException("爵位位阶必须唯一，无爵位须位于末尾。");
    }

    public bool IsCreatedRank(int id) => Ranks.Any(r => r.Id == id && !r.IsUnranked) &&
        !_baseDefinitions.Information.Ranks.Any(r => r.Id == id);

    public bool MoveRankTo(int id, int target)
    {
        ValidateRankOrder(Ranks);
        var ordered = OrderRanks(Ranks).ToList();
        int index = ordered.FindIndex(r => r.Id == id);
        if (index < 0) throw new InvalidDataException("找不到爵位。");
        if (ordered[index].IsUnranked) throw new InvalidDataException("无爵位固定在末尾。");
        if (target < 0 || target >= ordered.Count - 1) throw new ArgumentOutOfRangeException(nameof(target));
        if (target == index) return false;
        int[] precedence = ordered.Select(r => r.Precedence).ToArray();
        var moving = ordered[index]; ordered.RemoveAt(index); ordered.Insert(target, moving);
        var positions = ordered.Select((r, i) => (r.Id, Precedence: precedence[i])).ToDictionary(r => r.Id, r => r.Precedence);
        Definitions = Definitions with { Information = Definitions.Information with
            { Ranks = Ranks.Select(r => r with { Precedence = positions[r.Id] }).ToArray() } };
        return true;
    }

    public void RestoreRank(int id)
    {
        var baseline = _rankBaselines[id];
        Definitions = Definitions with { Information = Definitions.Information with
        {
            Ranks = Ranks.Select(r => (r.Id == id ? baseline : r) with { Precedence = _rankOrderBaseline[r.Id] }).ToArray()
        } };
    }

    public async Task<RankDefinition> CreateRankAsync(int? copyFrom = null, CancellationToken token = default)
    {
        ValidateRankOrder(Ranks);
        var draft = Definitions;
        var source = copyFrom is int original ? GetRank(original) : null;
        int id = await AllocateDefinitionIdentityAsync(DefinitionKind.Rank, token);
        EnsureDefinitionDraft(draft, token);
        var rank = source is null ? new RankDefinition(id, "新爵位", 10000, 0, 0, 0) :
            source with { Id = id, Name = source.Name + "副本", IsUnranked = false };
        int? after = source is { IsUnranked: false } ? source.Id : null;
        var entries = InsertRank(Ranks, rank, after).ToDictionary(r => r.Id);
        var orderBaseline = InsertRank(Ranks.Select(r => r with { Precedence = _rankOrderBaseline[r.Id] }), rank, after)
            .ToDictionary(r => r.Id, r => r.Precedence);
        rank = entries[id];
        Definitions = draft with { NextRankId = (long)id + 1, Information = draft.Information with
            { Ranks = [.. draft.Information.Ranks.Select(r => entries[r.Id]), rank] } };
        _rankBaselines.Add(id, rank); _rankOrderBaseline = orderBaseline;
        return rank;
    }

    private static RankDefinition[] InsertRank(IEnumerable<RankDefinition> ranks, RankDefinition rank, int? after)
    {
        var ordered = OrderRanks(ranks).ToList();
        int position = after is int id ? ordered.FindIndex(r => r.Id == id) + 1 : ordered.Count - 1;
        ordered.Insert(position, rank);
        int above = ordered[position - 1].Precedence, below = ordered[position + 1].Precedence;
        if ((long)above - below > 1) ordered[position] = rank with { Precedence = above - 1 };
        else ordered = ordered.Select((r, i) => r with { Precedence = ordered.Count - 1 - i }).ToList();
        return ordered.ToArray();
    }

    public async Task DeleteRankAsync(int id, CancellationToken token = default)
    {
        if (!IsCreatedRank(id)) throw new InvalidDataException("仅能删除自建正式爵位，原有项和无爵位不能删除。");
        var draft = Definitions;
        var ranks = Ranks.Where(r => r.Id != id).ToArray();
        ValidateRankOrder(ranks);
        var candidate = draft with { Information = draft.Information with { Ranks = ranks } };
        await ValidateDefinitionDeletionAsync(draft, candidate, "爵位", token);
        _rankBaselines.Remove(id); _rankOrderBaseline.Remove(id);
    }
}
