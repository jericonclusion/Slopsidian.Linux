using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using YamlDotNet.Core;

namespace RonSijm.Slopsidian.Linux;

internal static class NativeYamlScalar
{
    private static readonly Regex IntegerPattern = new(
        @"\A[+-]?[0-9][0-9_]*\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );
    private static readonly Regex NumberPattern = new(
        @"\A[+-]?(?:[0-9][0-9_]*(?:\.[0-9_]*)?|\.[0-9_]+)(?:[eE][+-]?[0-9]+)?\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );
    private static readonly Regex HexPattern = new(
        @"\A[+-]?0x[0-9a-fA-F_]+\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );
    private static readonly Regex OctalPattern = new(
        @"\A[+-]?0o[0-7_]+\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );

    public static object? Value(NativeYamlNode node)
    {
        var value = node.Resolved;
        if (value.Kind != NativeYamlKind.Scalar)
        {
            throw new InvalidDataException("Expected a YAML scalar.");
        }
        return Value(value.Text, value.Style, value.Tag);
    }

    public static object? Value(string text, ScalarStyle style, string tag)
    {
        if (tag is "!" or "tag:yaml.org,2002:str")
        {
            return text;
        }
        if (tag == "tag:yaml.org,2002:null")
        {
            if (!Null(text))
            {
                throw new InvalidDataException("Invalid explicitly tagged YAML null.");
            }
            return null;
        }
        if (tag == "tag:yaml.org,2002:bool")
        {
            return Boolean(text) ?? throw new InvalidDataException("Invalid explicitly tagged YAML boolean.");
        }
        if (tag is "tag:yaml.org,2002:int" or "tag:yaml.org,2002:float")
        {
            return TaggedNumber(text, tag);
        }
        if (style != ScalarStyle.Plain)
        {
            return text;
        }
        if (Null(text))
        {
            return null;
        }
        return Boolean(text) is { } boolean ? boolean : Number(text) ?? (object)text;
    }

    private static object TaggedNumber(string text, string tag)
    {
        var number = Number(text) ?? throw new InvalidDataException("Invalid explicitly tagged YAML number.");
        if (tag.EndsWith(":int", StringComparison.Ordinal))
        {
            return number is BigInteger
                ? number
                : throw new InvalidDataException("Invalid explicitly tagged YAML integer.");
        }
        return number is BigInteger integer ? (double)integer : number;
    }

    private static object? Number(string text)
    {
        var unsigned = text.TrimStart('+', '-');
        if (unsigned is ".inf" or ".Inf" or ".INF")
        {
            return text.StartsWith('-') ? double.NegativeInfinity : double.PositiveInfinity;
        }
        if (unsigned is ".nan" or ".NaN" or ".NAN")
        {
            return double.NaN;
        }
        if (IntegerPattern.IsMatch(text))
        {
            return ParseInteger(text);
        }
        if (HexPattern.IsMatch(text))
        {
            return ParseRadix(text, 16);
        }
        if (OctalPattern.IsMatch(text))
        {
            return ParseRadix(text, 8);
        }
        return
            NumberPattern.IsMatch(text)
            && double.TryParse(
                text.Replace("_", "", StringComparison.Ordinal),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number
            )
            ? number
            : null;
    }

    private static BigInteger ParseInteger(string value)
    {
        if (value.Length > 4096)
        {
            throw new InvalidDataException("A numeric scalar exceeds its digit limit.");
        }
        return BigInteger.Parse(
            value.Replace("_", "", StringComparison.Ordinal),
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture
        );
    }

    private static BigInteger ParseRadix(string value, int radix)
    {
        if (value.Length > 4096)
        {
            throw new InvalidDataException("A numeric scalar exceeds its digit limit.");
        }
        var negative = value.StartsWith('-');
        var unsigned = value.TrimStart('+', '-')[2..].Replace("_", "", StringComparison.Ordinal);
        var result = BigInteger.Zero;
        foreach (var character in unsigned)
        {
            var digit = char.IsAsciiDigit(character) ? character - '0' : char.ToLowerInvariant(character) - 'a' + 10;
            result = result * radix + digit;
        }
        return negative ? -result : result;
    }

    private static bool Null(string text) => text is "" or "~" or "null" or "Null" or "NULL";

    private static bool? Boolean(string text) =>
        text switch
        {
            "true" or "True" or "TRUE" => true,
            "false" or "False" or "FALSE" => false,
            _ => null,
        };
}
