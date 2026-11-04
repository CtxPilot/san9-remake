using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using San9.Application;
using San9.Application.Diagnostics;
using San9.Application.Persistence;
using San9.Content.Packages;
using San9.Core.Diagnostics;
using San9.Core.Domain;
using San9.Core.Simulation;

namespace San9.Tools.ScenarioBenchmark;

public sealed record MilitaryBaselineOptions
{
    public required string ContentPath { get; init; }
    public required string ReportPath { get; init; }
    public required string CodeRevision { get; init; }
    public ulong Seed { get; init; } = 281;
    public int StartXun { get; init; }
    public int Xuns { get; init; } = 36;
    public int Repetitions { get; init; } = 3;
    public int WarmupPasses { get; init; } = 1;
    public bool OtherForceOrdersVisible { get; init; } = true;
    public bool OtherForceTacticsVisible { get; init; } = true;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ContentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ReportPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(CodeRevision);
        if (StartXun < 0 || Xuns < 1 || Repetitions < 1 || WarmupPasses < 0)
            throw new ArgumentException("Invalid duration, repetitions or warmup settings.");
    }
}

public sealed record BaselineWork(string Work, int Reports, int Attempts, int Submitted, int Rejected,
    int EmptyPlans, double Milliseconds, long AllocatedBytes, int PathQueries, long PathSearches,
    long PathExpanded, long PathEndpointExpanded)
{
    public string Executor { get; init; } = "original";
    public int GroupEvaluations { get; init; }
}

public sealed record BaselineXun(string Started, string Ended, double Milliseconds, double PreparationMilliseconds,
    double ResolutionMilliseconds, long AllocatedBytes, long UnscopedSearches, long UnscopedExpanded, long UnscopedEndpointExpanded,
    long UnscopedMaximumExpanded, long UnscopedWorkspaceNodes, int UnitsBefore, int UnitsAfter, int MaximumUnits,
    int EventCount, BaselineWork[] Work);

public sealed record BaselinePass(string Kind, int Index, double RestoreMilliseconds, BaselineXun[] Xuns,
    long ManagedHeapBytes, long WorkingSetBytes)
{
    public double TotalMilliseconds => Xuns.Sum(x => x.Milliseconds);
}

/// <summary>Isolated measurement entry; all actions use the production session and command pipeline.</summary>
public static class MilitaryBaseline
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string configurationPath, TextWriter log)
    {
        if (typeof(MilitaryBaseline).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
                .OfType<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration != "Release")
            throw new InvalidOperationException("Run military-baseline in Release configuration.");
        var options = JsonSerializer.Deserialize<MilitaryBaselineOptions>(
            await File.ReadAllTextAsync(configurationPath, Encoding.UTF8), Json)
            ?? throw new ArgumentException("Missing benchmark configuration.");
        options.Validate();
        // Paths in the configuration are relative to that file, independent of the caller's cwd.
        string root = Path.GetDirectoryName(Path.GetFullPath(configurationPath))!;
        options = options with { ContentPath = Path.GetFullPath(options.ContentPath, root),
            ReportPath = Path.GetFullPath(options.ReportPath, root) };
        string inputFile = Directory.Exists(options.ContentPath)
            ? Path.Combine(options.ContentPath, "content.json") : options.ContentPath;
        if (string.Equals(options.ReportPath, inputFile, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(options.ReportPath, Path.GetFullPath(configurationPath), StringComparison.OrdinalIgnoreCase) ||
            options.ReportPath.StartsWith(Path.TrimEndingDirectorySeparator(Directory.Exists(options.ContentPath)
                ? options.ContentPath : Path.GetDirectoryName(options.ContentPath)!) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Report must be outside the content directory and must not overwrite the configuration.");

        var load = Stopwatch.StartNew();
        var content = await ScenarioContentFiles.LoadScenarioAsync(options.ContentPath);
        double loadMilliseconds = load.Elapsed.TotalMilliseconds;
        var startup = Stopwatch.StartNew();
        var initial = GameSession.Start(content, options.Seed, playerForceController: ControllerKind.Automatic);
        var settings = initial.Apply(initial.Request(new PkSettingsOperation(options.OtherForceOrdersVisible,
            options.OtherForceTacticsVisible, initial.Snapshot.Simulation.BattleDeathsEnabled)));
        if (settings.Disposition != SessionDisposition.Accepted) throw new InvalidOperationException(settings.Rejection.ToString());
        double startupMilliseconds = startup.Elapsed.TotalMilliseconds;
        for (int i = 0; i < options.StartXun; i++) Advance(initial, false);
        if (!initial.Snapshot.Clock.IsXunBoundary || initial.Snapshot.Simulation.Boundary != SimulationBoundary.Stable ||
            initial.Snapshot.Simulation.ActiveControl != 0)
            throw new InvalidOperationException("Benchmark requires a live, stable pre-order xun boundary.");
        byte[] checkpoint = SaveService.Encode(initial);
        var start = initial.Snapshot.Clock;
        List<BaselinePass> passes = [];
        SessionSnapshot? expectedFinal = null;
        List<IReadOnlyList<DomainEvent>> expectedEvents = [];
        var environment = new
        {
            runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount,
            cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"), serverGc = System.Runtime.GCSettings.IsServerGC,
            tieredCompilation = Setting("TieredCompilation"), dynamicPgo = Setting("TieredPGO"),
            readyToRun = Setting("ReadyToRun"), stopwatchFrequency = Stopwatch.Frequency
        };
        // No forced GC, PGO changes or wall-clock limits in the production simulation.
        Measure("first", 0);
        for (int i = 0; i < options.WarmupPasses; i++) Measure("warmup", i + 1);
        for (int i = 0; i < options.Repetitions; i++) Measure("sample", i + 1);
        WriteReport();
        log.WriteLine("Completed; exact final state and per-xun events verified. Timing stability is observational, not a development gate.");
        return 0;

        BaselinePass Measure(string kind, int index)
        {
            var restore = Stopwatch.StartNew();
            var session = SaveService.Decode(checkpoint, content);
            double restoreMilliseconds = restore.Elapsed.TotalMilliseconds;
            var xuns = new BaselineXun[options.Xuns];
            for (int i = 0; i < xuns.Length; i++)
            {
                var (sample, events) = Advance(session, true);
                xuns[i] = sample;
                // Compare after the timer; retain only the first trajectory's events and final state.
                if (kind == "first") expectedEvents.Add(events);
                else if (EventDiagnostics.Compare(expectedEvents[i], events) is { } difference)
                    throw new InvalidOperationException($"Events diverged in {kind} {index}, xun {i + 1}: {difference}.");
            }
            // Non-forcing process/heap readings, not peak memory or isolated simulation retention.
            long heapBytes = GC.GetTotalMemory(false);
            long workingSetBytes = Environment.WorkingSet;
            var final = session.Capture();
            if (expectedFinal is null) expectedFinal = final;
            else if (SessionDiagnostics.Compare(expectedFinal, final) is { } difference)
                throw new InvalidOperationException($"Final session diverged in {kind} {index}: {difference}.");
            var pass = new BaselinePass(kind, index, restoreMilliseconds, xuns, heapBytes, workingSetBytes);
            passes.Add(pass);
            WriteReport();
            log.WriteLine($"{kind}={index} xuns={xuns.Length} total_ms={pass.TotalMilliseconds:F2} " +
                $"p95_ms={Percentile(xuns.Select(x => x.Milliseconds), .95):F2} max_units={xuns.Max(x => x.MaximumUnits)}");
            return pass;
        }

        void WriteReport()
        {
            var samples = passes.Where(p => p.Kind == "sample").ToArray();
            var perXun = samples.SelectMany(p => p.Xuns).ToArray();
            var report = new
            {
                schemaVersion = 1, options, rulesVersion = SessionProtocol.RulesVersion,
                saveFormatVersion = SessionProtocol.SaveFormatVersion, configuration = "Release", environment,
                scenario = content.ScenarioId, started = start.ToString(), loadMilliseconds, startupMilliseconds,
                warmupPasses = options.WarmupPasses,
                timingQualification = "Fixed bounded sampling; no automatic steady-state claim. " +
                    "Interpret time and memory together with deterministic work counts and algorithmic cost.",
                cachePolicy = "Each pass decodes the same pre-order save into a new session; session/query caches reset. " +
                    "Runtime JIT, PGO and process-level workspaces remain warm. No warm business-cache result is claimed.",
                measurement = "PrepareAdvance plus ten AdvanceDay calls, including public submissions and session publication. " +
                    "Timers exclude load, restore, diagnostics comparison, event aggregation and report I/O. " +
                    "AI work time is included in preparation, not additive; resolution includes all daily rules, not only combat. " +
                    "Allocations are current-thread cumulative bytes. Heap/working set are end-of-pass readings, not peaks; " +
                    "they include loaded content and the retained reference trajectory. MaximumUnits is sampled after operations, not sub-day. " +
                    "Path diagnostic scopes are exclusive: total searches/expanded = unscoped counters + all Work path counters. " +
                    "Unscoped maxima/workspaces exclude nested AI scopes. No file hashes are computed.",
                verification = "Full events at every xun and exact final session compared with the first pass; no trajectory hashes.",
                summary = samples.Length == 0 ? null : new
                {
                    repetitions = samples.Length, totalMilliseconds = samples.Select(p => p.TotalMilliseconds).ToArray(),
                    medianTotalMilliseconds = Percentile(samples.Select(p => p.TotalMilliseconds), .5),
                    medianXunMilliseconds = Percentile(perXun.Select(x => x.Milliseconds), .5),
                    p95XunMilliseconds = Percentile(perXun.Select(x => x.Milliseconds), .95),
                    maximumXunMilliseconds = perXun.Max(x => x.Milliseconds),
                    meanAllocatedBytesPerXun = perXun.Average(x => (double)x.AllocatedBytes)
                },
                passes
            };
            Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
            File.WriteAllText(options.ReportPath, JsonSerializer.Serialize(report, Json) + "\n", new UTF8Encoding(false));
        }
    }

    private static string Setting(string name) => Environment.GetEnvironmentVariable("DOTNET_" + name)
        ?? Environment.GetEnvironmentVariable("COMPlus_" + name) ?? "runtime-default";

    public static (BaselineXun Sample, IReadOnlyList<DomainEvent> Events) Advance(GameSession session, bool diagnostics)
    {
        var started = session.Snapshot.Clock;
        if (!started.IsXunBoundary || session.Snapshot.Simulation.Boundary != SimulationBoundary.Stable ||
            session.Snapshot.Simulation.ActiveControl != 0)
            throw new InvalidOperationException("Expected a live pre-order xun boundary.");
        var results = new SessionResult[11];
        int unitsBefore = session.Snapshot.Units.Count, maximumUnits = unitsBefore;
        using var paths = diagnostics ? new PathSearchDiagnostics() : null;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long begin = Stopwatch.GetTimestamp();
        results[0] = Apply(new PrepareAdvanceOperation());
        var work = session.LastOriginalAiWork;
        long prepared = Stopwatch.GetTimestamp();
        for (int i = 1; i <= 10; i++) results[i] = Apply(new AdvanceDayOperation());
        long ended = Stopwatch.GetTimestamp();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        if (session.Snapshot.Clock.DayNumber != started.DayNumber + 10 ||
            session.Snapshot.Simulation.Boundary != SimulationBoundary.Stable)
            throw new InvalidOperationException("Run terminated before completing a full xun; no partial sample is accepted.");
        var events = results.SelectMany(r => r.Events).ToArray();
        var works = work.GroupBy(r => (r.Work, r.Executor)).OrderBy(g => g.Key.Work).ThenBy(g => g.Key.Executor, StringComparer.Ordinal).Select(g => new BaselineWork(g.Key.Work.ToString(),
            g.Count(), g.Sum(r => r.Attempts), g.Sum(r => r.Submitted), g.Sum(r => r.Rejected), g.Sum(r => r.EmptyPlans),
            g.Sum(r => r.ElapsedTimestampTicks) * 1000.0 / Stopwatch.Frequency, g.Sum(r => r.AllocatedBytes),
            g.Sum(r => r.PathQueries), g.Sum(r => r.PathSearches), g.Sum(r => r.PathExpanded), g.Sum(r => r.PathEndpointExpanded))
            { Executor = g.Key.Executor, GroupEvaluations = g.Sum(r => r.GroupEvaluations) }).ToArray();
        return (new(started.ToString(), session.Snapshot.Clock.ToString(), (ended - begin) * 1000.0 / Stopwatch.Frequency,
            (prepared - begin) * 1000.0 / Stopwatch.Frequency, (ended - prepared) * 1000.0 / Stopwatch.Frequency,
            allocatedBytes, paths?.Queries ?? 0, paths?.Expanded ?? 0, paths?.EndpointExpanded ?? 0,
            paths?.MaximumExpanded ?? 0, paths?.WorkspaceNodes ?? 0, unitsBefore, session.Snapshot.Units.Count,
            maximumUnits, events.Length, works), events);

        SessionResult Apply(SessionOperation operation)
        {
            var result = session.Apply(session.Request(operation));
            if (result.Disposition != SessionDisposition.Accepted)
                throw new InvalidOperationException($"{operation.GetType().Name}: {result.Rejection} {result.AutomaticResponseFailure}");
            maximumUnits = Math.Max(maximumUnits, session.Snapshot.Units.Count);
            return result;
        }
    }

    // Linear interpolation over the sorted samples (also used for the reported median).
    public static double Percentile(IEnumerable<double> values, double probability)
    {
        if (!double.IsFinite(probability) || probability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(probability));
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0) throw new ArgumentException("Expected nonempty samples.", nameof(values));
        double position = (sorted.Length - 1) * probability;
        int low = (int)position, high = (int)Math.Ceiling(position);
        return sorted[low] + (sorted[high] - sorted[low]) * (position - low);
    }
}
