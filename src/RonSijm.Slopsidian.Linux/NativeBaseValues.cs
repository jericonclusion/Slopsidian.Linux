using System.Globalization;
using System.Numerics;
using RonSijm.Slopsidian.Services;

namespace RonSijm.Slopsidian.Linux;

public sealed record NativeBaseDate(long Milliseconds)
{
    public DateTimeOffset Local => DateTimeOffset.FromUnixTimeMilliseconds(Milliseconds).ToLocalTime();
}

public sealed record NativeBaseLink(string Path, string Display);

public sealed record NativeBaseFile(
    VaultFile File,
    IReadOnlyDictionary<string, object?> Properties,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Links
)
{
    public IReadOnlyDictionary<string, string> PropertyTypes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public string? MetadataError { get; init; }
    public string Path => File.Path;
}

public sealed class NativeBaseBudget(int remaining = 1_000_000)
{
    public int Remaining { get; private set; } = remaining;

    public void Spend(int amount = 1)
    {
        if (amount < 0 || amount > Remaining)
        {
            Remaining = -1;
            throw new InvalidDataException("Bases query exceeded its evaluation budget.");
        }
        Remaining -= amount;
    }
}

public static class NativeBaseValues
{
    private static readonly CompareInfo Comparison = CultureInfo.GetCultureInfo("en").CompareInfo;

    public static void CheckName(string name)
    {
        if (name is "constructor" or "prototype" or "__proto__")
        {
            throw new InvalidDataException("This identifier is unavailable in Bases: " + name);
        }
    }

    public static bool IsNumber(object? value) =>
        value
            is double
                or float
                or decimal
                or int
                or long
                or short
                or byte
                or uint
                or ulong
                or ushort
                or sbyte
                or BigInteger;

    public static double Number(object? value)
    {
        if (!IsNumber(value))
        {
            throw new InvalidDataException("Expected a finite, safely representable number.");
        }
        var result = value is BigInteger integer
            ? (double)integer
            : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (!double.IsFinite(result) || (double.IsInteger(result) && Math.Abs(result) > 9_007_199_254_740_991d))
        {
            throw new InvalidDataException("Expected a finite, safely representable number.");
        }
        return result;
    }

    public static string Text(object? value)
    {
        if (value is not string text)
        {
            throw new InvalidDataException("Expected a string.");
        }
        return BoundedText(text);
    }

    public static string BoundedText(string value)
    {
        if (value.Length > 65_536)
        {
            throw new InvalidDataException("A formula string exceeds 65,536 characters.");
        }
        return value;
    }

    public static bool Truth(object? value) =>
        value switch
        {
            null => false,
            bool boolean => boolean,
            string text => text.Length != 0,
            _ when IsNumber(value) => Number(value).CompareTo(0d) != 0,
            _ => true,
        };

    public static string ScalarText(object? value) =>
        value switch
        {
            null => "",
            string text => BoundedText(text),
            bool boolean => boolean ? "true" : "false",
            NativeBaseDate date => date.Local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            NativeBaseLink link => BoundedText(link.Display),
            _ when IsNumber(value) => NumberText(Number(value)),
            _ => throw new InvalidDataException("This structured value cannot be converted to text."),
        };

    public static string Display(object? value, int depth = 0)
    {
        if (value is string text)
        {
            return Truncate(text, 2000);
        }
        if (value is NativeBaseLink link)
        {
            return Truncate(link.Display, 2000);
        }
        if (depth > 8)
        {
            return "Structured value";
        }
        if (value is IReadOnlyList<object?> list)
        {
            return Truncate(string.Join(", ", list.Take(128).Select(item => Display(item, depth + 1))), 2000);
        }
        return value is null or string or bool or NativeBaseDate or NativeBaseLink || IsNumber(value)
            ? Truncate(ScalarText(value), 2000)
            : "Structured value";
    }

    public static bool Equal(object? left, object? right, int depth = 0)
    {
        if (depth > 32)
        {
            throw new InvalidDataException("A value is nested too deeply to compare.");
        }
        if (left is null || right is null)
        {
            return left is null && right is null;
        }
        if (left is NativeBaseDate a && right is NativeBaseDate b)
        {
            return a.Milliseconds == b.Milliseconds;
        }
        if (left is NativeBaseLink x && right is NativeBaseLink y)
        {
            return x.Path == y.Path;
        }
        if (IsNumber(left) && IsNumber(right))
        {
            return Number(left).CompareTo(Number(right)) == 0;
        }
        if (left is IReadOnlyList<object?> first && right is IReadOnlyList<object?> second)
        {
            return EqualLists(first, second, depth);
        }
        return left is string or bool ? left.Equals(right) : ReferenceEquals(left, right);
    }

    private static bool EqualLists(IReadOnlyList<object?> left, IReadOnlyList<object?> right, int depth) =>
        left.Count == right.Count
        && left.Select((item, index) => Equal(item, right[index], depth + 1)).All(equal => equal);

    public static int Compare(object? left, object? right)
    {
        if (left is null)
        {
            return right is null ? 0 : 1;
        }
        if (right is null)
        {
            return -1;
        }
        if (left is NativeBaseDate a && right is NativeBaseDate b)
        {
            return a.Milliseconds.CompareTo(b.Milliseconds);
        }
        if (IsNumber(left) && IsNumber(right))
        {
            return Number(left).CompareTo(Number(right));
        }
        if (left is bool x && right is bool y)
        {
            return x.CompareTo(y);
        }
        return Comparison.Compare(Display(left), Display(right), CompareOptions.NumericOrdering);
    }

    private static string NumberText(double value)
    {
        if (value.CompareTo(0d) == 0)
        {
            return "0";
        }
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var separator = text.IndexOf('E');
        if (separator < 0)
        {
            return text;
        }
        var exponent = int.Parse(text[(separator + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var mantissa = text[..separator];
        if (exponent is < -6 or >= 21)
        {
            return mantissa + "e" + (exponent >= 0 ? "+" : "") + exponent.ToString(CultureInfo.InvariantCulture);
        }
        var sign = mantissa.StartsWith('-') ? "-" : "";
        var unsigned = sign.Length == 0 ? mantissa : mantissa[1..];
        var point = unsigned.IndexOf('.');
        var position = (point < 0 ? unsigned.Length : point) + exponent;
        var digits = unsigned.Replace(".", "", StringComparison.Ordinal);
        return sign + FixedDigits(digits, position);
    }

    private static string FixedDigits(string digits, int position)
    {
        if (position <= 0)
        {
            return "0." + new string('0', -position) + digits;
        }
        return position >= digits.Length
            ? digits + new string('0', position - digits.Length)
            : digits[..position] + "." + digits[position..];
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
