using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using San9.Application;
using San9.Application.AI.Military;
using San9.Application.Persistence;
using San9.Content.Packages;
using San9.Content.Scenarios;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Tools.ScenarioBenchmark;

public sealed record MilitaryFoundationOptions(string ReportPath, string CodeRevision, string? ContentPath = null, ulong Seed = 281);
public sealed record FoundationTiming(double Milliseconds, long AllocatedBytes);
public sealed record FoundationSpatialCase(string Name, int Objects, FoundationTiming Build, FoundationTiming DirectScan,
    FoundationTiming Queries, FoundationTiming Update, long ScanChecks, long IndexChecks, long BucketReferences,
    int UpdatedObjects, bool MatchesScan);
public sealed record FoundationDeliveryCase(int SourceId, int ImmediateTargetId, int FinalTargetId, int? DirectDays,
    int? FrontTransportDays, int OfficerReadyDay, int? AssembledAttackDay, int? NearDays, int? RemoteDays,
    int CompanionLimitDays, bool NearPassesCompanionControl, bool RemotePassesCompanionControl, bool EmptyFrontHasNoGroup);
public sealed record FoundationCombatProbe(int FormationId, long OwnLoss, long OpponentLoss, int Exchanges, int DamageCalls,
    uint FirstOutgoing, uint ActualFirstOutgoing, bool MatchesFirstExchange)
{
    public long FirstExchangeAbsoluteError => Math.Abs((long)FirstOutgoing - ActualFirstOutgoing);
}
public sealed record FoundationGroupCase(int BaselineFormation, int ImprovedFormation, int BaselineEvaluations,
    int SearchEvaluations, int EvaluationLimit, int ProbeDamageCallLimit, int ProbeChoice,
    bool BaselinePrepared, bool ImprovedPrepared, FoundationTiming BaselineCost, FoundationTiming SearchCost,
    FoundationTiming ProbeCost, FoundationTiming ProbeVerificationCost, ImmutableArray<FoundationCombatProbe> Probes)
{
    public bool OnlineProbeAdopted => false;
}
public sealed record MilitaryFoundationReport(ImmutableArray<FoundationSpatialCase> Spatial, FoundationDeliveryCase Delivery,
    FoundationGroupCase Groups);
public sealed record FoundationRouteSample(FoundationTiming Cost, int Searches, long Expanded, int InitializedCells);
public sealed record FoundationRouteCase(string Profile, int Targets, ImmutableArray<FoundationRouteSample> Pairwise,
    ImmutableArray<FoundationRouteSample> Shared, int MaximumEstimatedDayDifference, long RetainedRouteCells);

/// <summary>Small, bounded M0 comparisons. These use self-authored controlled inputs and public rules;
/// the arithmetic probe deliberately does not become an online combat simulator.</summary>
public static class MilitaryFoundationExperiments
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<int> RunAsync(string configurationPath, TextWriter log)
    {
        if (typeof(MilitaryFoundationExperiments).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
                .OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration != "Release")
            throw new InvalidOperationException("Run military-foundations in Release configuration.");
        var options = JsonSerializer.Deserialize<MilitaryFoundationOptions>(await File.ReadAllTextAsync(configurationPath, Encoding.UTF8), Json)
            ?? throw new ArgumentException("Missing foundation experiment configuration.");
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ReportPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CodeRevision);
        string root = Path.GetDirectoryName(Path.GetFullPath(configurationPath))!;
        string output = Path.GetFullPath(options.ReportPath, root);
        if (string.Equals(output, Path.GetFullPath(configurationPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Report must not overwrite the input configuration.");
        var experiments = RunControlled();
        object? scenario = null;
        if (options.ContentPath is { } input)
        {
            string contentPath = Path.GetFullPath(input, root);
            string directory = Directory.Exists(contentPath) ? contentPath : Path.GetDirectoryName(contentPath)!;
            if (output.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Report must be outside the content directory.");
            var content = await ScenarioContentFiles.LoadScenarioAsync(contentPath);
            var session = GameSession.Start(content, options.Seed, playerForceController: ControllerKind.Automatic);
            object Capture(string name)
            {
                int owner = session.Snapshot.Forces.Where(f => !f.IsDormant && !f.IsEliminated && !f.IsBarbarian).OrderBy(f => f.Id).First().Id;
                MilitaryObservation? observation = null;
                var projection = Measure(() => observation = session.ObserveMilitary(owner));
                var world = session.Snapshot;
                var save = SaveService.Encode(session);
                var restored = SaveService.Decode(save, content);
                if (San9.Application.Diagnostics.SessionDiagnostics.Compare(session, restored) is { } difference)
                    throw new InvalidOperationException($"Fixed scene restoration differed: {difference}.");
                return new { name, clock = world.Clock.ToString(), owner, fieldUnits = world.Units.Count(u => !u.IsEmbedded && !u.IsPersonnel),
                    activeForces = world.Forces.Count(f => !f.IsDormant && !f.IsEliminated),
                    publicTargets = observation!.Units.Where(u => u.Order.IsKnown && u.Order.Value is not null)
                        .Select(u => u.Order.Value).Distinct().Count(), projection, index = observation.Forces.Work, exactRestore = true };
            }
            var opening = Capture("opening");
            for (int i = 0; i < 12; i++) session.AdvanceXun();
            scenario = new { content.ScenarioId, options.Seed, opening, fixedScene = Capture("after-12-xun") };
        }
        var report = new
        {
            schemaVersion = 1, options, rulesVersion = SessionProtocol.RulesVersion, saveFormatVersion = SessionProtocol.SaveFormatVersion,
            fixture = "military-foundations-v1", configuration = "Release", runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            processors = Environment.ProcessorCount, cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
            tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime-default",
            dynamicPgo = Environment.GetEnvironmentVariable("DOTNET_TieredPGO") ?? "runtime-default", experiments, scenario,
            routes = RouteCosts(),
            limits = "Single bounded observations; no timing convergence claim, file hashes, forced GC or online policy switch. " +
                "Spatial stress measures queries, not full thousand-unit turns. Probe assumes a known field defender, " +
                "no tactics, no reinforcements and two ordinary paired exchanges; the first outgoing loss is checked " +
                "against the public simulation pipeline. It is not a win probability. " +
                "Evaluation and damage-call ceilings are separately counted operations, not claims of equal CPU cost. " +
                "Companion travel control is an experiment, never a runtime eligibility gate. Route comparison uses one explicit warmup " +
                "and two alternating measured pairs, a shared synthetic map, and both speed ratios. It compares coarse estimates, not daily arrivals."
        };
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, Json) + "\n", new UTF8Encoding(false));
        log.WriteLine($"Foundation experiments passed; report={output}");
        return 0;
    }

    public static MilitaryFoundationReport RunControlled()
    {
        var spatial = new[] { Spatial("ordinary", 16), Spatial("multiple-fronts", 128), Spatial("many-units", 2048) }.ToImmutableArray();
        var content = Fixture();
        var session = GameSession.Start(content, 281);
        var rules = new MilitaryRulebook(content.Military!);
        var queries = session.MilitaryQueries(1, new(100000, 4, 8));
        var direct = queries.FromSettlement(1, new(3, true), 10, null, 5000);
        var transport = queries.FromSettlement(1, new(2, true), 30, null, 5000, arrival: NavigationArrival.Region);
        var finalLeg = queries.FromSettlement(2, new(3, true), 10, null, 5000);
        var near = queries.Transport(1, [2], 10, null, 5000, horizonDays: 120).Routes[0];
        var remote = queries.Transport(1, [3], 40, null, 5000, horizonDays: 400).Routes[0];
        var force = session.Snapshot.Forces.Single(f => f.Id == 1);
        var abilities = OfficerAbilityRules.Empty;
        var people = session.Snapshot.Officers.Where(p => p.OwnerId == 1 && p.SettlementId == 1)
            .Select(p => MilitaryGroupEvaluator.ObserveOwnOfficer(p, force, abilities, [], TreasureRulebook.Empty, rules)).ToArray();
        var evaluator = new MilitaryGroupEvaluator(rules);
        bool emptyFront = evaluator.Search(force, people, [new(10)], 2, 90000, 10000, new(47, 7), MilitaryGroupRole.Field).Best is null;
        var move = new MoveOfficersCommand(SimulationIdentity.Command(session.Snapshot.Simulation.NextSequence, 1), 1,
            session.Snapshot.Simulation.NextSequence, [3], 2);
        var moved = session.Execute(new("player"), move);
        if (!moved.Submission.Accepted) throw new InvalidOperationException($"Personnel experiment rejected: {moved.Submission.Rejection}.");
        int readyDay = 0;
        while (readyDay < 100 && !(session.Snapshot.Officers.Single(p => p.Id == 3) is { SettlementId: 2, Acted: false } &&
                !session.Snapshot.Simulation.Tasks.Any(t => t.OfficerId == 3)))
        { session.AdvanceXun(); readyDay += 10; }
        if (readyDay >= 100) throw new InvalidOperationException("Personnel did not become available at a public order boundary.");
        int? assembled = transport.Days is int delivery && finalLeg.Days is int onward
            ? Math.Max(readyDay, (delivery + 9) / 10 * 10) + onward : null;
        int companionLimit = Math.Max(50, (direct.Days ?? 0) + 10);
        var deliveryCase = new FoundationDeliveryCase(1, 2, 3, direct.Days, transport.Days, readyDay, assembled,
            near.Days, remote.Days, companionLimit, near.Days <= companionLimit, remote.Days <= companionLimit, emptyFront);
        return new(spatial, deliveryCase, GroupCase(content, rules));
    }

    public static ImmutableArray<FoundationRouteCase> RouteCosts()
    {
        const int width = 96, height = 24;
        var ids = Enumerable.Range(0, width * height).Select(i => i / width is >= 8 and <= 15 && i % width is >= 20 and <= 75 ? 1 : 0).ToImmutableArray();
        var result = ImmutableArray.CreateBuilder<FoundationRouteCase>();
        foreach (int shipSpeed in new[] { 20, 2 })
        {
            var rules = new MilitaryRulebook(new(width, height, ids, [new(0, 1, null), new(1, null, 1) { IsWater = true }],
                [new(10, 20, 0, 10000, 1, new(1, 1), TravelMode.Land), new(20, shipSpeed, 0, 10000, 1, new(1, 1), TravelMode.Water)], [])
                { BasicNavalFormationId = 20 });
            var travel = rules.Travel(10, 20, 1000);
            var start = new WorldPosition(2, 62);
            foreach (int count in new[] { 1, 4, 16 })
            {
                var targets = Enumerable.Range(0, count).Select(id => new TravelFieldEndpoint(id,
                    new(id == 0 ? new(477, 62) : new WorldPosition((80 + id % 16) * 5 + 2, (1 + id % 22) * 5 + 2), new(1, 1)))).ToArray();
                RouteCostEstimate[] oldRoutes = [];
                BattlefieldTravelField shared = null!;
                FoundationRouteSample Pairwise()
                {
                    using var paths = new PathSearchDiagnostics();
                    var time = Measure(() => oldRoutes = targets.Select(t => travel.Map.EstimateTravelCost(start, t.Area, travel.Shape,
                        travel.Mode, [], new(100000), arrival: NavigationArrival.Point, includeCells: true)).ToArray());
                    return new(time, (int)paths.Queries, paths.Expanded, width * height * count);
                }
                FoundationRouteSample Shared()
                {
                    var work = new NavigationWorkBudget(262144);
                    var time = Measure(() => shared = BattlefieldTravelField.Build(travel.Map, [start], targets, travel.Mode,
                        120L * travel.DailyBudget, work));
                    return new(time, 1, shared.Expanded, shared.CellsInitialized);
                }
                Pairwise(); Shared(); // Fixed warmup, never an adaptive convergence loop.
                var oldSamples = ImmutableArray.CreateBuilder<FoundationRouteSample>();
                var sharedSamples = ImmutableArray.CreateBuilder<FoundationRouteSample>();
                oldSamples.Add(Pairwise()); sharedSamples.Add(Shared());
                sharedSamples.Add(Shared()); oldSamples.Add(Pairwise());
                if (!shared.Complete || shared.Routes.Any(r => r.Status != TravelFieldStatus.Reachable) ||
                    oldRoutes.Any(r => r.Status != RouteReachability.Reachable)) throw new InvalidOperationException("Route comparison did not finish.");
                int difference = targets.Select((_, i) => (int)Math.Abs(
                    (oldRoutes[i].Cost + travel.DailyBudget - 1) / travel.DailyBudget -
                    (shared.Routes[i].Cost + travel.DailyBudget - 1) / travel.DailyBudget)).Max();
                result.Add(new($"land20-water{shipSpeed}", count, oldSamples.ToImmutable(), sharedSamples.ToImmutable(),
                    difference, shared.Routes.Sum(r => (long)r.Cells.Length)));
            }
        }
        return result.ToImmutable();
    }

    private static FoundationSpatialCase Spatial(string name, int count)
    {
        var values = Enumerable.Range(0, count).Select(id => new MilitaryPresence(new(id, false), MilitaryPresenceKind.Unit,
            1 + id % 4, 1 + id % 8, new(new(id % 64 * 100, id / 64 * 100), new(1, 1)), 1000, 100)).ToArray();
        MilitaryForceIndex index = null!;
        var build = Measure(() => index = MilitaryForceIndex.Create(values));
        var areas = values.Take(16).Select(p => new MapOccupancy(p.Area.Position, new(3, 3))).ToArray();
        var expected = new List<HashSet<MapObjectReference>>();
        var direct = Measure(() => { foreach (var area in areas) expected.Add(values.Where(p => p.Area.Overlaps(area)).Select(p => p.Object).ToHashSet()); });
        long checkedObjects = 0;
        bool matches = true;
        var queries = Measure(() =>
        {
            for (int i = 0; i < areas.Length; i++)
            {
                var found = index.Nearby(areas[i]); checkedObjects += found.ObjectsChecked;
                matches &= expected[i].SetEquals(found.Objects.Select(p => p.Object));
            }
        });
        var changed = index.ForForce(1).Select(p => p.Object.Id == 0 ? p with { Area = new(new(50, 50), new(1, 1)), Wounded = 200 } : p).ToArray();
        MilitaryForceIndex updated = null!;
        var update = Measure(() => updated = index.ReplaceForce(1, changed));
        var rebuilt = MilitaryForceIndex.Create(values.Select(p => p.Object.Id == 0 ? changed[0] : p));
        matches &= updated.Objects.SequenceEqual(rebuilt.Objects);
        if (!matches) throw new InvalidOperationException("Spatial index differed from direct scan or rebuild.");
        return new(name, count, build, direct, queries, update, (long)count * areas.Length, checkedObjects,
            index.Work.BucketReferences, updated.Work.UpdatedObjects, matches);
    }

    private static FoundationGroupCase GroupCase(ScenarioInitialState content, MilitaryRulebook rules)
    {
        var session = GameSession.Start(content, 281);
        var world = session.Snapshot;
        var force = world.Forces.Single(f => f.Id == 1);
        var people = world.Officers.Where(p => p.OwnerId == 1 && p.SettlementId == 1).Select(p =>
            MilitaryGroupEvaluator.ObserveOwnOfficer(p, force, OfficerAbilityRules.Empty, [], TreasureRulebook.Empty, rules)).ToArray();
        var evaluator = new MilitaryGroupEvaluator(rules);
        MilitaryGroupEvaluation baseline = null!, search = null!;
        var baselineTime = Measure(() => baseline = evaluator.Search(force, people, [new(10)], 1, 10000, 50000,
            new(7, 7), MilitaryGroupRole.Field, maximumEvaluations: 8));
        var searchTime = Measure(() => search = evaluator.Search(force, people, [new(10), new(20)], 1, 10000, 50000,
            new(7, 7), MilitaryGroupRole.Field, maximumEvaluations: 8));
        if (baseline.Best is null || search.Best is null) throw new InvalidOperationException("No baseline organization in the controlled fixture.");
        bool Prepared(MilitaryGroupOption option)
        {
            var order = new MilitaryOrder(MilitaryOrderKind.AttackSettlement, null, 3);
            var members = option.Members.OrderBy(p => p.OfficerId == option.CommanderId ? 0 : 1).ThenBy(p => p.OfficerId).ToArray();
            var command = new DeployUnitCommand(SimulationIdentity.Command(world.Simulation.NextSequence, 1), 1, world.Simulation.NextSequence,
                1, members.Select(p => p.OfficerId).ToImmutableArray(), option.Soldiers, option.FormationId, order)
            { Positions = members.ToImmutableDictionary(p => p.OfficerId, p => p.Position), Tactics = members.ToImmutableDictionary(p => p.OfficerId, p => p.TacticId) };
            var validation = session.Validate(new("player"), command);
            if (!validation.Accepted) throw new InvalidOperationException($"Formation {option.FormationId} preparation rejected: {validation.Rejection}.");
            return true;
        }
        bool baselinePrepared = Prepared(baseline.Best), improvedPrepared = Prepared(search.Best);
        var probes = ImmutableArray.CreateBuilder<FoundationCombatProbe>();
        var probeTime = Measure(() =>
        {
            foreach (var option in new[] { baseline.Best, search.Best }.DistinctBy(p => p.FormationId))
            {
                var own = new CombatStrength(option.Soldiers, 100, people.Single(p => p.Officer.Id == option.CommanderId).Abilities.Leadership, 1, 10);
                var opponent = new CombatStrength(5000, 100, 70, 1, 10);
                var profile = rules.Formations[option.FormationId].Combat!;
                var targetProfile = new CombatProfile(25, 20, 10, 20, 10);
                long ownLoss = 0, opponentLoss = 0; int calls = 0, exchanges = 0;
                uint firstOutgoing = 0;
                for (; exchanges < 2 && own.Soldiers > 0 && opponent.Soldiers > 0; exchanges++)
                {
                    uint damage = Math.Min(opponent.Soldiers, CombatMath.TroopDamage(own, opponent, profile, targetProfile, false, 0)); calls++;
                    if (exchanges == 0) firstOutgoing = damage;
                    // The public combat pipeline captures the outgoing/counter pair before either loss.
                    uint response = Math.Min(own.Soldiers, CombatMath.TroopDamage(opponent, own, targetProfile, profile, false, 0)); calls++;
                    opponent = opponent with { Soldiers = opponent.Soldiers - damage }; opponentLoss += damage;
                    own = own with { Soldiers = own.Soldiers - response }; ownLoss += response;
                }
                probes.Add(new(option.FormationId, ownLoss, opponentLoss, exchanges, calls, firstOutgoing, 0, false));
            }
        });
        var probeVerificationTime = Measure(() =>
        {
            for (int i = 0; i < probes.Count; i++)
            {
                var option = probes[i].FormationId == baseline.Best.FormationId ? baseline.Best : search.Best;
                uint actual = ActualFirstOutgoing(option, new(25, 20, 10, 20, 10));
                probes[i] = probes[i] with { ActualFirstOutgoing = actual, MatchesFirstExchange = actual == probes[i].FirstOutgoing };
            }
        });
        var chosen = probes.OrderByDescending(p => p.OpponentLoss - p.OwnLoss).ThenBy(p => p.FormationId).First();
        if (!baselinePrepared || !improvedPrepared || search.Best.Score < baseline.Best.Score)
            throw new InvalidOperationException("Organization experiment violated public preparation or lost its baseline.");
        return new(baseline.Best.FormationId, search.Best.FormationId, baseline.Evaluations, search.Evaluations, 8, 8,
            chosen.FormationId, baselinePrepared, improvedPrepared, baselineTime, searchTime, probeTime, probeVerificationTime, probes.ToImmutable());

        uint ActualFirstOutgoing(MilitaryGroupOption option, CombatProfile defenderProfile)
        {
            var defender = new FormationRule(50, 10, 0, 5000, 1, new(1, 1), TravelMode.Land)
                { Positions = [new(0, BattleRank.Middle)], Attack = new(1, true), Combat = defenderProfile };
            var memberIds = option.Members.Select(p => p.OfficerId).ToHashSet();
            var battle = content with
            {
                Military = content.Military! with { Formations = content.Military.Formations.Add(defender) },
                Units = [new(0, 1, new(37, 7), option.Soldiers)
                    { FormationId = option.FormationId, Morale = 100, CommanderId = option.CommanderId,
                        MemberPositions = option.Members.ToImmutableDictionary(p => p.OfficerId, p => p.Position),
                        ServicePlaceId = 1, CorpsId = 1, Order = new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 1 } },
                    new(1, 2, new(38, 7), 5000) { FormationId = 50, Morale = 100, CommanderId = 2,
                        MemberPositions = ImmutableDictionary<int, FormationPosition>.Empty.Add(2, new(0, BattleRank.Middle)),
                        ServicePlaceId = 3, CorpsId = 2, Order = new(MilitaryOrderKind.AttackUnit, null, null) { UnitId = 0 } }],
                Officers = content.Officers.Select(p => memberIds.Contains(p.State.Id) ? p with
                    { State = p.State with { SettlementId = null, UnitId = 0 } } : p.State.Id == 2 ? p with
                    { State = p.State with { SettlementId = null, UnitId = 1 } } : p).ToArray()
            };
            var execution = GameSession.Start(battle, 281, playerForceController: ControllerKind.Automatic);
            execution.Apply(execution.Request(new PrepareAdvanceOperation()));
            var result = execution.Apply(execution.Request(new AdvanceDayOperation()));
            return result.Events.OfType<BattleDamageEvent>().First(e => !e.AttackerIsSettlement && e.AttackerId == 0 && e.TargetUnitId == 1 && e.TacticId is null).SoldierLoss;
        }
    }

    private static FoundationTiming Measure(Action action)
    {
        long allocated = GC.GetAllocatedBytesForCurrentThread(), started = Stopwatch.GetTimestamp();
        action();
        return new(Stopwatch.GetElapsedTime(started).TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocated);
    }

    private static ScenarioInitialState Fixture()
    {
        ImmutableArray<FormationPosition> positions = [new(0, BattleRank.Middle), new(1, BattleRank.Middle)];
        var ordinary = new FormationRule(10, 10, 50, 5000, 2, new(1, 1), TravelMode.Land)
            { Positions = positions, Attack = new(1, true), Combat = new(20, 20, 10, 20, 10) };
        return new(ScenarioInitialState.CurrentSchemaVersion, "military-foundations-v1", 190, 1, 1, 1,
            [new(1, "own", 1, 100) { LeaderId = 1 }, new(2, "opponent", 2, 100) { LeaderId = 2 }],
            [new(1, "rear", 1, 1, 1, 10000, 1000, 1000, 100, []),
             new(2, "front", 1, 9, 1, 90000, 1000, 1000, 100, []),
             new(3, "target", 2, 19, 1, 5000, 1000, 1000, 100, [])], [], [])
        {
            Officers = [new("leader", new(1, 1, 1, new(90, 70, 70, 95), false) { Status = OfficerStatus.Ruler, BirthYear = 160 }),
                new("opponent", new(2, 2, 3, new(70, 70, 70, 70), false) { Status = OfficerStatus.Ruler, BirthYear = 160 }),
                new("general", new(3, 1, 1, new(80, 80, 70, 85), false) { BirthYear = 160 })],
            Military = new(20, 3, Enumerable.Repeat(0, 60).ToImmutableArray(), [new(0, 1, null)],
                [ordinary, ordinary with { Id = 20, DeploymentCost = 100, Combat = new(80, 40, 20, 40, 10) },
                 ordinary with { Id = 30, MovementBudget = 40, SoldierCapacity = 20000 }, ordinary with { Id = 40, MovementBudget = 1 }],
                [new(1, new(7, 7), new(1, 1)) { Combat = new(20, 20, 10, 20, 10) },
                 new(2, new(47, 7), new(1, 1)) { Combat = new(20, 20, 10, 20, 10) },
                 new(3, new(97, 7), new(1, 1)) { Combat = new(20, 20, 10, 20, 10) }]) { TransportFormations = new(30, null) },
            Information = new([], [], [], [new(1, 1, 1, new(50000, 1000000)) { IsRoot = true }, new(2, 2, 2, new(50000, 1000000)) { IsRoot = true }], []),
            OriginalAi = OriginalAiDefinition.Default with { WorkRules = OriginalAiDefinition.Default.WorkRules.ToImmutableDictionary(p => p.Key,
                p => p.Value with { OpeningWaitDays = 10000 }) }
        };
    }
}
