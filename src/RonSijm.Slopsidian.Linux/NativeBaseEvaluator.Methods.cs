using System.Globalization;

namespace RonSijm.Slopsidian.Linux;

public sealed partial class NativeBaseEvaluator
{
    private object? Global(string name, object?[] arguments)
    {
        if (arguments.Length == 0)
        {
            return name switch
            {
                "now" => NativeBaseDates.Create(now),
                "today" => NativeBaseDates.StartOfDay(NativeBaseDates.Create(now)),
                _ => throw Unsupported(name),
            };
        }
        if (name is "min" or "max")
        {
            var values = arguments.Select(NativeBaseValues.Number).ToArray();
            return name == "min" ? values.Min() : values.Max();
        }
        if (name == "link" && arguments.Length <= 2)
        {
            return Link(arguments);
        }
        if (arguments.Length != 1)
        {
            throw Unsupported(name);
        }
        return name switch
        {
            "date" => NativeBaseDates.Parse(NativeBaseValues.Text(arguments[0])),
            "number" => ConvertNumber(arguments[0]),
            "list" => arguments[0] is IReadOnlyList<object?> list ? list : [arguments[0]],
            _ => throw Unsupported(name),
        };
    }

    private object? Method(object? receiver, string name, object?[] arguments)
    {
        NativeBaseValues.CheckName(name);
        if (receiver is IReadOnlyList<object?> list)
        {
            CheckList(list);
        }
        if (summaryValues is not null && receiver is null && name is "round" or "toFixed" or "format")
        {
            return null;
        }
        if (IsUniversalMethod(name, arguments.Length))
        {
            return UniversalMethod(receiver, name, arguments);
        }
        if (receiver is string or IReadOnlyList<object?> && name is "contains" or "containsAll" or "containsAny")
        {
            return Contains(receiver, name, arguments);
        }
        return receiver switch
        {
            NativeBaseFile source => FileMethod(source, name, arguments),
            NativeBaseDate date => DateMethod(date, name, arguments),
            string text => StringMethod(text, name, arguments),
            IReadOnlyList<object?> items => ListMethod(items, name, arguments),
            _ when NativeBaseValues.IsNumber(receiver) => NumberMethod(
                NativeBaseValues.Number(receiver),
                name,
                arguments
            ),
            _ => throw Unsupported(name),
        };
    }

    private static bool IsUniversalMethod(string name, int count) =>
        count == 0 && name is "isEmpty" or "isTruthy" or "toString" || count == 1 && name == "isType";

    private static object UniversalMethod(object? receiver, string name, object?[] arguments) =>
        name switch
        {
            "isEmpty" => receiver is null or "" || receiver is IReadOnlyList<object?> { Count: 0 },
            "isTruthy" => NativeBaseValues.Truth(receiver),
            "toString" => NativeBaseValues.ScalarText(receiver),
            _ => IsType(receiver, NativeBaseValues.Text(arguments[0])),
        };

    private static bool IsType(object? value, string type) =>
        type switch
        {
            "null" => value is null,
            "list" => value is IReadOnlyList<object?>,
            "date" => value is NativeBaseDate,
            "link" => value is NativeBaseLink,
            "number" => NativeBaseValues.IsNumber(value),
            "string" => value is string,
            "boolean" => value is bool,
            "object" => value is not (string or bool) && !NativeBaseValues.IsNumber(value),
            _ => false,
        };

    private object FileMethod(NativeBaseFile source, string name, object?[] arguments)
    {
        if (name == "asLink" && arguments.Length <= 1)
        {
            return new NativeBaseLink(
                source.Path,
                arguments.Length == 0 || arguments[0] is null
                    ? source.File.Basename
                    : NativeBaseValues.ScalarText(arguments[0])
            )
            {
                ExactPath = true,
            };
        }
        if (arguments.Length != 1)
        {
            throw Unsupported(name);
        }
        if (name == "hasLink")
        {
            return HasLink(source, arguments[0]);
        }
        var text = NativeBaseValues.Text(arguments[0]);
        return name switch
        {
            "hasTag" => source.Tags.Any(tag =>
                StripHash(tag) == StripHash(text)
                || StripHash(tag).StartsWith(StripHash(text) + "/", StringComparison.Ordinal)
            ),
            "inFolder" => text.Trim('/').Length == 0
                || source.Path.StartsWith(text.Trim('/') + "/", StringComparison.Ordinal),
            "hasProperty" => ReadProperties(source).ContainsKey(text),
            _ => throw Unsupported(name),
        };
    }

    private bool HasLink(NativeBaseFile source, object? argument)
    {
        var target = argument switch
        {
            NativeBaseFile note => note.Path,
            NativeBaseLink link => link.Path,
            _ => WithoutWikiBrackets(NativeBaseValues.Text(argument)),
        };
        budget.Spend(files.Count);
        var matches = files
            .Keys.Where(path =>
                path == target
                || WithoutMarkdownExtension(path) == target
                || WithoutMarkdownExtension(System.IO.Path.GetFileName(path)) == target
            )
            .Take(2)
            .ToArray();
        return source.Links.Contains(matches.Length == 1 ? matches[0] : target, StringComparer.Ordinal);
    }

    private object DateMethod(NativeBaseDate date, string name, object?[] arguments)
    {
        if (name == "format" && arguments.Length == 1)
        {
            var pattern = NativeBaseValues.Text(arguments[0]);
            budget.Spend(pattern.Length);
            return NativeBaseDates.Format(date, pattern);
        }
        if (arguments.Length != 0)
        {
            throw Unsupported(name);
        }
        return name switch
        {
            "date" => NativeBaseDates.StartOfDay(date),
            "time" => date.Local.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            _ => throw Unsupported(name),
        };
    }

    private static object NumberMethod(double value, string name, object?[] arguments)
    {
        if (arguments.Length == 0 && name is "abs" or "floor" or "ceil")
        {
            return name switch
            {
                "abs" => Math.Abs(value),
                "floor" => Math.Floor(value),
                _ => Math.Ceiling(value),
            };
        }
        if (arguments.Length > 1 || name is not ("round" or "toFixed"))
        {
            throw Unsupported(name);
        }
        var digits = arguments.Length == 0 || arguments[0] is null ? 0 : NativeBaseValues.Number(arguments[0]);
        if (digits < 0 || digits > 12 || !double.IsInteger(digits))
        {
            throw new InvalidDataException("Use 0 to 12 decimal places.");
        }
        var rounded = Math.Round(value, (int)digits, MidpointRounding.AwayFromZero);
        if (name == "round")
        {
            return NativeBaseValues.Number(rounded);
        }
        if (value.CompareTo(0d) == 0)
        {
            rounded = 0d;
        }
        return rounded.ToString(
            "F" + ((int)digits).ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture
        );
    }

    private static object StringMethod(string value, string name, object?[] arguments)
    {
        NativeBaseValues.BoundedText(value);
        if (arguments.Length == 0)
        {
            return name switch
            {
                "lower" => value.ToLowerInvariant(),
                "upper" => value.ToUpperInvariant(),
                "trim" => value.Trim(),
                _ => throw Unsupported(name),
            };
        }
        if (name == "slice")
        {
            var (start, count) = SliceRange(value.Length, arguments);
            return value.Substring(start, count);
        }
        if (name == "replace" && arguments.Length == 2)
        {
            return Replace(value, NativeBaseValues.Text(arguments[0]), NativeBaseValues.Text(arguments[1]));
        }
        if (arguments.Length != 1)
        {
            throw Unsupported(name);
        }
        var text = NativeBaseValues.Text(arguments[0]);
        return name switch
        {
            "startsWith" => value.StartsWith(text, StringComparison.Ordinal),
            "endsWith" => value.EndsWith(text, StringComparison.Ordinal),
            "split" => Split(value, text),
            _ => throw Unsupported(name),
        };
    }

    private static object?[] Split(string value, string separator)
    {
        var parts =
            separator.Length == 0
                ? value.Select(character => character.ToString()).Take(1025).ToArray()
                : value.Split(separator, 1025, StringSplitOptions.None);
        if (parts.Length > 1024)
        {
            throw new InvalidDataException("Formula lists exceed 1,024 elements.");
        }
        return parts.Cast<object?>().ToArray();
    }

    private static string Replace(string value, string oldValue, string newValue)
    {
        var result = new System.Text.StringBuilder();
        var start = 0;
        if (oldValue.Length == 0)
        {
            AppendReplacement(result, value, newValue, 0, 0);
            for (var index = 0; index < value.Length; index++)
            {
                Append(result, value[index].ToString());
                AppendReplacement(result, value, newValue, index + 1, 0);
            }
            return result.ToString();
        }
        while (start < value.Length)
        {
            var next = value.IndexOf(oldValue, start, StringComparison.Ordinal);
            if (next < 0)
            {
                Append(result, value[start..]);
                break;
            }
            Append(result, value[start..next]);
            AppendReplacement(result, value, newValue, next, oldValue.Length);
            start = next + oldValue.Length;
        }
        return result.ToString();
    }

    private static void AppendReplacement(
        System.Text.StringBuilder result,
        string source,
        string replacement,
        int offset,
        int length
    )
    {
        var literal = 0;
        var position = 0;
        while (position + 1 < replacement.Length)
        {
            if (replacement[position] != '$')
            {
                position++;
                continue;
            }
            var marker = replacement[position + 1];
            if (marker is not ('$' or '&' or '`' or '\''))
            {
                position++;
                continue;
            }
            Append(result, replacement[literal..position]);
            var value = marker switch
            {
                '$' => "$",
                '&' => source.Substring(offset, length),
                '`' => source[..offset],
                _ => source[(offset + length)..],
            };
            Append(result, value);
            position += 2;
            literal = position;
        }
        Append(result, replacement[literal..]);
    }

    private bool Contains(object? receiver, string name, object?[] arguments)
    {
        if (name == "contains" && arguments.Length != 1)
        {
            throw Unsupported(name);
        }
        if (receiver is IReadOnlyList<object?> values)
        {
            budget.Spend(values.Count * Math.Max(1, arguments.Length));
        }
        bool Includes(object? item) =>
            receiver is string text
                ? text.Contains(NativeBaseValues.Text(item), StringComparison.Ordinal)
                : ((IReadOnlyList<object?>)receiver!).Any(entry => NativeBaseValues.Equal(entry, item));
        return name == "containsAll" ? arguments.All(Includes) : arguments.Any(Includes);
    }

    private static NativeBaseLink Link(object?[] arguments)
    {
        var path = arguments[0] is NativeBaseFile source
            ? source.Path
            : WithoutWikiBrackets(NativeBaseValues.Text(arguments[0]));
        if (
            System.Text.RegularExpressions.Regex.IsMatch(
                path,
                @"\A[a-z][a-z0-9_+.-]*:",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
                    | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)
            )
            && !path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidDataException("Unsupported link scheme.");
        }
        var parts = path.Split('|');
        var display = parts.Length > 1 ? parts[1] : WithoutMarkdownExtension(parts[0]);
        if (arguments.Length > 1 && arguments[1] is not null)
        {
            display = NativeBaseValues.ScalarText(arguments[1]);
        }
        return new NativeBaseLink(parts[0], display) { ExactPath = arguments[0] is NativeBaseFile };
    }

    private static double ConvertNumber(object? value)
    {
        if (value is NativeBaseDate date)
        {
            return NativeBaseValues.Number(date.Milliseconds);
        }
        if (value is bool boolean)
        {
            return boolean ? 1d : 0d;
        }
        if (NativeBaseValues.IsNumber(value))
        {
            return NativeBaseValues.Number(value);
        }
        var text = NativeBaseValues.Text(value).Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return NativeBaseValues.Number(number);
        }
        try
        {
            return ParseRadix(text);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidDataException("number() needs a numeric value.", exception);
        }
    }

    private static double ParseRadix(string value)
    {
        if (value.Length < 3 || value[0] != '0')
        {
            throw new FormatException("Not a number.");
        }
        var radix = char.ToLowerInvariant(value[1]) switch
        {
            'x' => 16,
            'b' => 2,
            'o' => 8,
            _ => throw new FormatException("Not a number."),
        };
        return NativeBaseValues.Number(Convert.ToUInt64(value[2..], radix));
    }

    private static (int Start, int Count) SliceRange(int length, object?[] arguments)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw Unsupported("slice");
        }
        int Index(object? value)
        {
            var number = Math.Truncate(NativeBaseValues.Number(value));
            return (int)Math.Clamp(number < 0 ? length + number : number, 0, length);
        }
        var start = Index(arguments[0]);
        var end = arguments.Length == 1 || arguments[1] is null ? length : Index(arguments[1]);
        return (start, Math.Max(0, end - start));
    }

    private static void Append(System.Text.StringBuilder result, string text)
    {
        if (result.Length + text.Length > 65_536)
        {
            throw new InvalidDataException("A formula string exceeds 65,536 characters.");
        }
        result.Append(text);
    }

    private static string StripHash(string value) => value.StartsWith('#') ? value[1..] : value;

    private static string WithoutWikiBrackets(string value)
    {
        var start = value.StartsWith("[[", StringComparison.Ordinal) ? value[2..] : value;
        return start.EndsWith("]]", StringComparison.Ordinal) ? start[..^2] : start;
    }

    private static InvalidDataException Unsupported(string name) =>
        new("Unsupported function, method, type or argument count: " + name + "().");
}
