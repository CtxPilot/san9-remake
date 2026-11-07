using System.Globalization;

namespace San9.Application.Presentation;

public sealed class BoundedNumberInput(uint minimum, uint maximum, uint initial)
{
    public uint Minimum { get; } = minimum <= maximum ? minimum : throw new ArgumentOutOfRangeException(nameof(minimum));
    public uint Maximum { get; } = maximum;
    public string Text { get; private set; } = Math.Clamp(initial, minimum, maximum).ToString(CultureInfo.InvariantCulture);
    public uint Value => TryGetValue(out uint value) ? value : 0;
    public bool CanConfirm => TryGetValue(out uint value) && value >= Minimum && value <= Maximum;
    private bool _replaceOnNextDigit = true;
    private bool TryGetValue(out uint value) => uint.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    public void Edit(string text)
    {
        Text = text;
        _replaceOnNextDigit = false;
    }
    public void Append(string digits)
    {
        foreach (char digit in digits)
        {
            if (digit is < '0' or > '9') throw new ArgumentException("Digits required.", nameof(digits));
            if (_replaceOnNextDigit)
            {
                Text = "";
                _replaceOnNextDigit = false;
            }
            Text = Math.Min(Maximum, (ulong)Value * 10 + (uint)(digit - '0')).ToString(CultureInfo.InvariantCulture);
        }
    }
    public void Backspace()
    {
        Text = Text.Length > 0 ? Text[..^1] : "";
        _replaceOnNextDigit = false;
    }
    public void Clear()
    {
        Text = "";
        _replaceOnNextDigit = false;
    }
    public void Set(uint value)
    {
        Text = Math.Clamp(value, Minimum, Maximum).ToString(CultureInfo.InvariantCulture);
        _replaceOnNextDigit = true;
    }
    public void Step(int amount)
    {
        Text = Math.Clamp((long)Value + amount, Minimum, Maximum).ToString(CultureInfo.InvariantCulture);
        _replaceOnNextDigit = true;
    }
}
