namespace San9.Importer.San9PkTc101;

/// <summary>One immutable scenario input with per-operation parsed section reuse.</summary>
public sealed class ScenarioSource(byte[] bytes)
{
    private readonly Dictionary<Delegate, object> _records = [];
    public byte[] Bytes { get; } = bytes;

    public T Read<T>(Func<Stream, T> parser) where T : notnull
    {
        if (_records.TryGetValue(parser, out var value)) return (T)value;
        using var stream = new MemoryStream(Bytes, writable: false);
        T result = parser(stream);
        _records.Add(parser, result);
        return result;
    }
}
