using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

#if DEBUG
throw new InvalidOperationException("Run this benchmark in Release configuration.");
#endif
if (args.Length is < 2 or > 3)
    throw new ArgumentException("Usage: fixture-path report-path [runs]");
int runs = args.Length == 3 ? int.Parse(args[2]) : 10;
if (runs is < 2 or > 100) throw new ArgumentOutOfRangeException(nameof(runs));
const int warmups = 3;
byte[] inputBytes = File.ReadAllBytes(args[0]);
using var document = JsonDocument.Parse(inputBytes);
var root = document.RootElement;
var input = Input.Parse(root);
var expected = new[] { ReadExpected("bounded"), ReadExpected("allFacilities") };
var samples = new List<Sample>[] { [], [] };
var firstCalls = new Sample[2];
for (int mode = 0; mode < 2; mode++)
{
    firstCalls[mode] = Measure(mode);
    for (int i = 0; i < warmups; i++) Validate(Search.Build(input, mode == 0 ? input.BoundedDays : null), mode);
}
for (int iteration = 0; iteration < runs; iteration++)
{
    int first = iteration % 2;
    samples[first].Add(Measure(first));
    samples[1 - first].Add(Measure(1 - first));
}
var diagnostics = new[] { Search.Build(input, input.BoundedDays), Search.Build(input, null) };
Validate(diagnostics[0], 0);
Validate(diagnostics[1], 1);
var filtered = diagnostics[1].Relations.Where(e => e.Cost <= input.BoundedDays * input.TicksPerDay).ToArray();
if (!filtered.SequenceEqual(diagnostics[0].Relations)) throw new InvalidOperationException("Full/bounded mismatch.");
string[] names = ["bounded", "allFacilities"];
var reports = Enumerable.Range(0, 2).ToDictionary(mode => names[mode], mode => new
{
    firstCallMilliseconds = firstCalls[mode].Milliseconds,
    milliseconds = samples[mode].Select(s => s.Milliseconds).ToArray(),
    medianMilliseconds = Median(samples[mode].Select(s => s.Milliseconds)),
    minimumMilliseconds = samples[mode].Min(s => s.Milliseconds),
    maximumMilliseconds = samples[mode].Max(s => s.Milliseconds),
    medianAllocatedBytes = Median(samples[mode].Select(s => (double)s.AllocatedBytes)),
    directedRelations = diagnostics[mode].Relations.Length,
    settledCells = diagnostics[mode].SettledCells,
    maximumQueueLength = diagnostics[mode].MaximumQueueLength
});
string sourcePath = GetSourcePath();
byte[] sourceBytes = [.. File.ReadAllBytes(sourcePath), .. File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(sourcePath)!, "FacilityTravelBenchmark.csproj"))];
var report = new
{
    scenario = root.GetProperty("scenario").Clone(),
    sourceHash = root.GetProperty("sourceHash").GetString(),
    pythonModelHash = root.GetProperty("pythonModelHash").GetString(),
    fixtureHash = Convert.ToHexStringLower(SHA256.HashData(inputBytes)),
    csharpSourceHash = Convert.ToHexStringLower(SHA256.HashData(sourceBytes)),
    environment = new { runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
        architecture = RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount,
        serverGc = System.Runtime.GCSettings.IsServerGC,
        tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "runtime-default" },
    configuration = "Release", facilities = input.Nodes.Length, gridCells = input.Width * input.Height,
    boundedDays = input.BoundedDays, input.LandBudget, input.WaterBudget,
    runsPerMode = runs, warmupsPerMode = warmups,
    results = reports,
    medianTimeRatio = reports["allFacilities"].medianMilliseconds / reports["bounded"].medianMilliseconds,
    checks = new { allResultsMatchPython = true, boundedIsFullCostSubset = true, allRunsIdentical = true },
    excludes = new[] { "fileIO", "jsonSerialization", "routeConstruction", "aiRules", "processStartup", "build" },
    memoryMeaning = "Cumulative thread allocations per build, not resident or peak memory"
};
string output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
foreach (var (name, result) in reports)
    Console.WriteLine($"{name}: {result.directedRelations} relations, median {result.medianMilliseconds:F3} ms, " +
        $"range {result.minimumMilliseconds:F3}-{result.maximumMilliseconds:F3} ms, " +
        $"allocated {result.medianAllocatedBytes / 1048576:F3} MiB, expanded {result.settledCells}");
Console.WriteLine($"Python equivalence passed; report: {output}");

Expected[] ReadExpected(string mode) => root.GetProperty("expected").GetProperty(mode).EnumerateArray()
    .Select(e => new Expected(e.GetProperty("from").GetInt32(), e.GetProperty("to").GetInt32(), e.GetProperty("days").GetDouble())).ToArray();

Sample Measure(int mode)
{
    long allocated = GC.GetAllocatedBytesForCurrentThread();
    long started = Stopwatch.GetTimestamp();
    var result = Search.Build(input, mode == 0 ? input.BoundedDays : null);
    var sample = new Sample(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
        GC.GetAllocatedBytesForCurrentThread() - allocated);
    Validate(result, mode);
    return sample;
}

void Validate(Result result, int mode)
{
    var target = expected[mode];
    if (result.Relations.Length != target.Length) throw new InvalidOperationException("Python relation count differs.");
    for (int i = 0; i < target.Length; i++)
    {
        var actual = result.Relations[i];
        if (actual.From != target[i].From || actual.To != target[i].To ||
            Math.Round((double)actual.Cost / input.TicksPerDay, 6, MidpointRounding.ToEven) != target[i].Days)
            throw new InvalidOperationException($"Python cost differs at relation {i}.");
    }
}

static double Median(IEnumerable<double> values)
{
    var sorted = values.Order().ToArray();
    return (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
}

static string GetSourcePath([CallerFilePath] string path = "") => path;

readonly record struct Sample(double Milliseconds, long AllocatedBytes);
readonly record struct Expected(int From, int To, double Days);
readonly record struct Relation(int From, int To, long Cost);
readonly record struct Node(int Id, int Cell);
readonly record struct Terrain(int Id, bool IsWater, int? LandCost, int? WaterCost);
sealed record Result(Relation[] Relations, long SettledCells, int MaximumQueueLength);

sealed record Input(int Width, int Height, int[] TerrainIds, Terrain[] Terrains, int[] Blocked,
    Node[] Nodes, int LandBudget, int WaterBudget, int BoundedDays)
{
    public long TicksPerDay => (long)LandBudget / Gcd(LandBudget, WaterBudget) * WaterBudget;
    public static Input Parse(JsonElement root)
    {
        int width = root.GetProperty("width").GetInt32();
        static int? NullableCost(JsonElement e, string name) => e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetInt32();
        return new(width, root.GetProperty("height").GetInt32(),
            root.GetProperty("terrainIds").EnumerateArray().Select(e => e.GetInt32()).ToArray(),
            root.GetProperty("terrains").EnumerateArray().Select(e => new Terrain(e.GetProperty("id").GetInt32(),
                e.GetProperty("isWater").GetBoolean(), NullableCost(e, "landCost"), NullableCost(e, "waterCost"))).ToArray(),
            root.GetProperty("blockedCells").EnumerateArray().Select(e => e.GetProperty("y").GetInt32() * width + e.GetProperty("x").GetInt32()).ToArray(),
            root.GetProperty("nodes").EnumerateArray().Select(e => new Node(e.GetProperty("id").GetInt32(),
                e.GetProperty("y").GetInt32() * width + e.GetProperty("x").GetInt32())).ToArray(),
            root.GetProperty("landBudget").GetInt32(), root.GetProperty("waterBudget").GetInt32(), root.GetProperty("boundedDays").GetInt32());
    }
    private static int Gcd(int a, int b) { while (b != 0) (a, b) = (b, a % b); return a; }
}

static class Search
{
    public static Result Build(Input input, int? maximumDays)
    {
        int count = input.Width * input.Height;
        long ticks = input.TicksPerDay;
        long limit = maximumDays is int days ? days * ticks : long.MaxValue;
        var types = input.Terrains.ToDictionary(t => t.Id);
        var costs = new long[count];
        var blocked = new bool[count];
        foreach (int cell in input.Blocked) blocked[cell] = true;
        var heads = new int[count];
        Array.Fill(heads, -1);
        var next = new int[input.Nodes.Length];
        for (int i = input.Nodes.Length - 1; i >= 0; i--)
        {
            int cell = input.Nodes[i].Cell;
            next[i] = heads[cell]; heads[cell] = i;
        }
        var adjacent = new int[count * 4];
        Array.Fill(adjacent, -1);
        for (int i = 0; i < count; i++)
        {
            var terrain = types[input.TerrainIds[i]];
            int? cost = terrain.IsWater ? terrain.WaterCost : terrain.LandCost;
            costs[i] = cost is int value ? value * 5 * (ticks / (terrain.IsWater ? input.WaterBudget : input.LandBudget)) : -1;
            int x = i % input.Width, y = i / input.Width;
            if (y > 0) adjacent[i * 4] = i - input.Width;
            if (x + 1 < input.Width) adjacent[i * 4 + 1] = i + 1;
            if (y + 1 < input.Height) adjacent[i * 4 + 2] = i + input.Width;
            if (x > 0) adjacent[i * 4 + 3] = i - 1;
        }
        var distance = new long[count];
        var queue = new PriorityQueue<int, (long Cost, int Cell)>();
        var relations = new List<Relation>();
        long settled = 0;
        int peak = 0;
        foreach (var source in input.Nodes)
        {
            Array.Fill(distance, long.MaxValue);
            queue.Clear();
            distance[source.Cell] = 0;
            queue.Enqueue(source.Cell, (0, source.Cell));
            int remaining = input.Nodes.Length - 1;
            while (queue.TryDequeue(out int at, out var priority))
            {
                long cost = priority.Cost;
                if (cost != distance[at]) continue;
                if (cost > limit) break;
                settled++;
                for (int endpoint = heads[at]; endpoint != -1; endpoint = next[endpoint])
                {
                    int target = input.Nodes[endpoint].Id;
                    if (target == source.Id) continue;
                    relations.Add(new(source.Id, target, cost));
                    remaining--;
                }
                if (remaining == 0) break;
                if (at != source.Cell && blocked[at]) continue;
                for (int direction = 0; direction < 4; direction++)
                {
                    int target = adjacent[at * 4 + direction];
                    if (target < 0 || costs[target] < 0 || blocked[target] && heads[target] == -1) continue;
                    long candidate = cost + costs[target];
                    if (candidate > limit || candidate >= distance[target]) continue;
                    distance[target] = candidate;
                    queue.Enqueue(target, (candidate, target));
                }
                peak = Math.Max(peak, queue.Count);
            }
        }
        relations.Sort(static (a, b) => a.From != b.From ? a.From.CompareTo(b.From) : a.To.CompareTo(b.To));
        return new(relations.ToArray(), settled, peak);
    }
}
