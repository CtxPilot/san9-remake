namespace San9.Importer.San9PkTc101.Interface;

/// <summary>Expands TC101 scene groups into facility catalog references at the import boundary.</summary>
internal static class BattleFacilityTypes
{
    internal static IReadOnlyDictionary<int, int> Groups { get; } = new Dictionary<int, int>
    {
        [0] = 1,
        [1] = 0,
        [2] = 1, [3] = 1, [4] = 1, [5] = 1,
        [6] = 0,
        [7] = 2, [8] = 2,
        [9] = 1
    };
}
