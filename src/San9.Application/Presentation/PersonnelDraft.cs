using System.Collections.Immutable;
using San9.Content.Maps;
using San9.Content.Text;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Application.Presentation;

/// <summary>Revision-bound personnel preparation. Selections remain local until one atomic submission.</summary>
public sealed class PersonnelDraft
{
    private readonly GameSession _session;
    // Journey estimates and adviser judgments share the draft's fixed world revision.
    private readonly Dictionary<(int Actor, int Target), PersonnelJourneyPreview> _journeys = [];
    private readonly Dictionary<int, Dictionary<int, PersonnelAdvicePreview>> _advice = [];
    private readonly Dictionary<int, PersonnelAdviceGrade> _targetRecommendations = [];
    private readonly Dictionary<int, int> _politics;
    private bool HasAdvice => Kind is "search" or "employ";
    public StrategySnapshot View { get; }
    public SettlementState Home { get; }
    public string Kind { get; }
    public PersonnelJourneyKind JourneyKind => Kind switch { "search" => PersonnelJourneyKind.Search, "employ" => PersonnelJourneyKind.Recruit, _ => PersonnelJourneyKind.Move };
    public ImmutableArray<int> Candidates { get; }
    public ImmutableArray<int> Targets { get; }
    public ImmutableArray<int> Members { get; private set; } = [];
    public int? TargetId { get; private set; }
    private bool _membersEdited;
    private int? _representative;
    public int? RepresentativeId => HasAdvice
        ? _representative : Members.IsEmpty ? null : Members[0];
    public long RecommendationRevision { get; private set; }
    public int SelectionLimit => Candidates.Length;
    public bool IsCurrent => _session.Snapshot.Revision == View.World.Revision;
    public bool CanExecute => IsCurrent && TargetId is int target && !Members.IsEmpty && Members.All(id => Journey(id, target).Available);
    public CommandAdvice? Advice => RepresentativeId is int actor ? AdviceFor(actor) : null;
    public ThreeVisitsStep? SearchAdvice => RepresentativeId is int actor ? SearchAdviceFor(actor) : null;
    public static bool Supports(string kind) => kind is "summon" or "move" or "search" or "employ";
    public static string? UnavailableReasonKey(StrategySnapshot view, int homeId, string kind) => Supports(kind)
        ? new MapCommandAvailability(view).SettlementReasonKey(homeId, kind) : San9.Content.Interface.CommandMenuSkin.ReasonKey("unavailable");

    public PersonnelDraft(GameSession session, int homeId, string kind)
    {
        _session = session; View = session.View; Kind = kind;
        _politics = View.Officers.ToDictionary(p => p.Id, p => p.Abilities.Politics);
        if (UnavailableReasonKey(View, homeId, kind) is string reason) throw new ArgumentException(reason, nameof(homeId));
        Home = View.World.Settlements.Single(s => s.Id == homeId);
        var availability = new MapCommandAvailability(View);
        Candidates = availability.PersonnelActors(Home, kind)
            .Where(p => kind != "summon" || Journey(p.Id, homeId).Available)
            .OrderByDescending(p => _politics[p.Id])
            .ThenBy(p => p.Id).Select(p => p.Id).ToImmutableArray();
        Targets = kind == "summon" ? [homeId] : kind == "search" ? session.GetSearchPlaces(Home.OwnerId!.Value) :
            availability.PersonnelTargets(Home, kind).Order().ToImmutableArray();
        if (kind == "employ") Targets = Targets.Intersect(session.GetRecruitTargets(Home.OwnerId!.Value, Candidates[0])).ToImmutableArray();
        if (Targets.Length == 1) TargetId = Targets[0];
        if (HasAdvice) RefreshAdvisedSelection();
        else if (Candidates.Length == 1) Members = Candidates.Take(1).ToImmutableArray();
    }
    private PersonnelJourneyPreview Journey(int actor, int target)
    {
        if (!IsCurrent) throw new InvalidOperationException("Stale personnel preparation.");
        if (!_journeys.TryGetValue((actor, target), out var preview))
        {
            preview = _session.GetPersonnelJourney(Home.OwnerId!.Value, actor, target, JourneyKind, includeAdvice: false);
            _journeys.Add((actor, target), preview);
        }
        return preview;
    }
    private void EnsureAdvice(IEnumerable<int> actors, IEnumerable<int> targets)
    {
        if (!IsCurrent) throw new InvalidOperationException("Stale personnel preparation.");
        var missing = ImmutableArray.CreateBuilder<(int Actor, int Target)>();
        foreach (int actor in actors)
        {
            _advice.TryGetValue(actor, out var row);
            if (row?.Count == Targets.Length) continue;
            foreach (int target in targets)
                if (row?.ContainsKey(target) != true) missing.Add((actor, target));
        }
        if (missing.Count == 0) return;
        foreach (var (pair, advice) in _session.GetPersonnelAdvice(Home.OwnerId!.Value, JourneyKind, missing.ToImmutable()))
        {
            if (!_advice.TryGetValue(pair.Actor, out var row)) _advice.Add(pair.Actor, row = []);
            row.Add(pair.Target, advice);
        }
    }
    private PersonnelAdvicePreview PairAdvice(int actor, int target)
    {
        if (!_advice.TryGetValue(actor, out var row) || !row.TryGetValue(target, out var advice))
        {
            EnsureAdvice(Candidates, [target]);
            return _advice[actor][target];
        }
        return advice;
    }
    public CommandAdvice? AdviceFor(int actor) => IsCurrent && HasAdvice && Candidates.Contains(actor) && TargetId is int target
        ? PairAdvice(actor, target).Advice : null;
    public ThreeVisitsStep? SearchAdviceFor(int actor) => IsCurrent && Kind == "search" && Candidates.Contains(actor) && TargetId is int target
        ? PairAdvice(actor, target).SearchAdvice : null;
    public PersonnelAdviceGrade AdviceGrade(int actor) => IsCurrent && HasAdvice && Candidates.Contains(actor) && TargetId is int target
        ? PersonnelAdviceGrades.From(PairAdvice(actor, target)) : PersonnelAdviceGrade.None;
    public PersonnelAdviceGrade TargetAdviceGrade(int target)
    {
        if (!IsCurrent || !HasAdvice || !Targets.Contains(target)) return PersonnelAdviceGrade.None;
        if (_targetRecommendations.TryGetValue(target, out var grade)) return grade;
        var pool = _membersEdited ? Members : Candidates;
        EnsureAdvice(pool, Targets);
        grade = PersonnelAdviceGrades.Summarize(pool.Select(actor => _advice[actor][target]));
        _targetRecommendations.Add(target, grade);
        return grade;
    }
    public int AdvicePriority(int actor) => (int)AdviceGrade(actor);
    public int TargetAdvicePriority(int target) => (int)TargetAdviceGrade(target);
    public string AdviceSymbol(int actor) => PersonnelAdviceGrades.Symbol(AdviceGrade(actor));
    public string TargetAdviceSymbol(int target) => PersonnelAdviceGrades.Symbol(TargetAdviceGrade(target));
    public IEnumerable<int> OrderAdvisedOfficers(IEnumerable<int> ids) =>
        ids.OrderByDescending(AdviceGrade).ThenByDescending(id => _politics[id]).ThenBy(id => id);
    private void InvalidateRecommendations()
    {
        _targetRecommendations.Clear();
        RecommendationRevision++;
    }
    public int? ArrivalDays(int actor) => IsCurrent && TargetId is int target ? Journey(actor, target).Days : null;
    public int? ArrivalDaysTo(int target) => IsCurrent && Targets.Contains(target)
        ? Journey(Members.IsEmpty ? Candidates[0] : Members[0], target).Days : null;
    public int? DistanceDaysTo(int target) => IsCurrent && Targets.Contains(target)
        ? Journey(Members.IsEmpty ? Candidates[0] : Members[0], target).DistanceDays : null;
    public bool CanSelect(IReadOnlyCollection<int> ids) => IsCurrent && ids.Count <= SelectionLimit &&
        ids.Distinct().Count() == ids.Count && !ids.Except(Candidates).Any() &&
        (TargetId is not int target || ids.All(id => Journey(id, target).Available));
    public bool SetMembers(IEnumerable<int> ids)
    {
        int[] chosen = ids.ToArray();
        if (!CanSelect(chosen)) return false;
        Members = Candidates.Where(chosen.Contains).ToImmutableArray();
        _membersEdited = true;
        InvalidateRecommendations();
        if (HasAdvice) RefreshAdvisedSelection();
        return true;
    }
    public bool SetTarget(int id)
    {
        if (!IsCurrent || !Targets.Contains(id)) return false;
        TargetId = id;
        if (HasAdvice) RefreshAdvisedSelection();
        return true;
    }
    private void RefreshAdvisedSelection()
    {
        if (!_membersEdited)
        {
            var favorable = Candidates.Where(id => AdviceGrade(id) is PersonnelAdviceGrade.Favorable or PersonnelAdviceGrade.Special);
            Members = OrderAdvisedOfficers(favorable).Take(1).ToImmutableArray();
        }
        else if (TargetId is int target && Members.Any(id => !Journey(id, target).Available))
        {
            Members = Members.Where(id => Journey(id, target).Available).ToImmutableArray();
            InvalidateRecommendations();
        }
        _representative = OrderAdvisedOfficers(Members).Select(id => (int?)id).FirstOrDefault();
    }
    public ImmutableHashSet<MapCell> TargetCells(MapPackage map)
    {
        if (Kind == "move" && View.MilitaryRules is {} rules)
            return View.World.Settlements.Where(s => Targets.Contains(s.Id)).SelectMany(rules.GeographicFootprint).ToImmutableHashSet();
        if (Kind != "search") return [];
        var cells = ImmutableHashSet.CreateBuilder<MapCell>();
        var regions = Targets.ToHashSet();
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
            if (PlaceAt(map, new(x, y)) is int id && regions.Contains(id)) cells.Add(new(x, y));
        return cells.ToImmutable();
    }
    public int? PlaceAt(MapPackage map, MapCell cell)
    {
        if (cell.X < 0 || cell.X >= map.Width || cell.Y < 0 || cell.Y >= map.Height) return null;
        var square = map.Squares[cell.Y * map.Width + cell.X];
        return !square.HideRegionName && View.RegionNameKeys.ContainsKey(square.RegionId) ? square.RegionId : null;
    }
    public string PlaceName(TextCatalog texts, int id) => View.Settlements.SingleOrDefault(s => s.Id == id)?.ResolveName(texts)
        ?? (View.RegionNameKeys.TryGetValue(id, out string? key) ? texts.Resolve(key) : throw new InvalidDataException("Unknown personnel place."));
    public string DestinationName(TextCatalog texts, int id)
    {
        var place = View.Settlements.Single(s => s.Id == id);
        string name = place.ResolveName(texts);
        return place.Kind == SettlementKind.City ? name : name + "〈" + PlaceName(texts, View.RegionCityIds[id]) + "〉";
    }
    public GameCommand CreateCommand(Guid id, long sequence)
    {
        if (!CanExecute) throw new InvalidOperationException("Invalid or stale personnel preparation.");
        int owner = Home.OwnerId!.Value, target = TargetId!.Value;
        return Kind switch
        {
            "search" => new SearchCommand(id, owner, sequence, Members, target),
            "employ" => new RecruitOfficersCommand(id, owner, sequence, Members, target),
            _ => new MoveOfficersCommand(id, owner, sequence, Members, target)
        };
    }
}
