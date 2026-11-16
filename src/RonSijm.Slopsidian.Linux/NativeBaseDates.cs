using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RonSijm.Slopsidian.Linux;

internal static class NativeBaseDates
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly Regex IsoDate = new(
        @"\A[0-9]{4}-[0-9]{2}-[0-9]{2}(?:[T ][0-9]{2}:[0-9]{2}(?::[0-9]{2}(?:\.[0-9]+)?)?(?:Z|[+-][0-9]{2}:?[0-9]{2})?)?\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );
    private static readonly Regex WeekDate = new(
        @"\A(?<year>[0-9]{4})-?W(?<week>[0-9]{2})(?:-?(?<day>[1-7]))?\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );
    private static readonly Regex OrdinalDate = new(
        @"\A(?<year>[0-9]{4})-?(?<day>[0-9]{3})\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );
    private static readonly Regex Duration = new(
        @"\A(?<amount>[+-]?[0-9]+(?:\.[0-9]+)?)\s*(?<unit>y|years?|M|months?|w|weeks?|d|days?|h|hours?|m|minutes?|s|seconds?)\z",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );
    private static readonly string[] Tokens =
    [
        "YYYY",
        "MMMM",
        "dddd",
        "SSSS",
        "MMM",
        "ddd",
        "SSS",
        "YY",
        "MM",
        "DD",
        "Do",
        "HH",
        "hh",
        "mm",
        "ss",
        "SS",
        "ZZ",
        "WW",
        "Qo",
        "Y",
        "M",
        "D",
        "H",
        "h",
        "m",
        "s",
        "S",
        "A",
        "a",
        "Z",
        "X",
        "x",
        "d",
        "E",
        "W",
        "Q",
        "z",
    ];
    private static readonly Dictionary<string, string> Localized = new(StringComparer.Ordinal)
    {
        ["LTS"] = "h:mm:ss A",
        ["LT"] = "h:mm A",
        ["LLLL"] = "dddd, MMMM D, YYYY h:mm A",
        ["LLL"] = "MMMM D, YYYY h:mm A",
        ["LL"] = "MMMM D, YYYY",
        ["L"] = "MM/DD/YYYY",
        ["llll"] = "ddd, MMM D, YYYY h:mm A",
        ["lll"] = "MMM D, YYYY h:mm A",
        ["ll"] = "MMM D, YYYY",
        ["l"] = "M/D/YYYY",
    };

    public static NativeBaseDate Create(long milliseconds)
    {
        try
        {
            _ = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("Date is outside the supported range.", exception);
        }
        return new NativeBaseDate(milliseconds);
    }

    public static NativeBaseDate Parse(string source)
    {
        try
        {
            var normalized = NormalizeDate(source);
            if (
                !IsoDate.IsMatch(normalized)
                || !DateTimeOffset.TryParse(
                    normalized,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var date
                )
            )
            {
                throw new InvalidDataException("Invalid ISO date: " + source);
            }
            return Create(date.ToUnixTimeMilliseconds());
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("Invalid ISO date: " + source, exception);
        }
    }

    private static string NormalizeDate(string source)
    {
        var week = WeekDate.Match(source);
        if (week.Success)
        {
            return NormalizeWeek(week);
        }
        var ordinal = OrdinalDate.Match(source);
        if (ordinal.Success)
        {
            return NormalizeOrdinal(ordinal);
        }
        if (source.Length == 8 && source.All(char.IsAsciiDigit))
        {
            return source[..4] + "-" + source[4..6] + "-" + source[6..];
        }
        if (source.Length == 4 && source.All(char.IsAsciiDigit))
        {
            return source + "-01-01";
        }
        return source.Length == 7 && source[4] == '-' ? source + "-01" : source;
    }

    private static string NormalizeWeek(Match week)
    {
        var year = Integer(week.Groups["year"].Value);
        var number = Integer(week.Groups["week"].Value);
        if (number < 1 || number > ISOWeek.GetWeeksInYear(year))
        {
            throw new InvalidDataException("Invalid ISO week date.");
        }
        var day = week.Groups["day"].Success ? Integer(week.Groups["day"].Value) : 1;
        return ISOWeek
            .ToDateTime(year, number, day == 7 ? DayOfWeek.Sunday : (DayOfWeek)day)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string NormalizeOrdinal(Match ordinal)
    {
        var year = Integer(ordinal.Groups["year"].Value);
        var day = Integer(ordinal.Groups["day"].Value);
        if (day < 1 || day > (DateTime.IsLeapYear(year) ? 366 : 365))
        {
            throw new InvalidDataException("Invalid ISO ordinal date.");
        }
        return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)
            .AddDays(day - 1)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static NativeBaseDate Add(NativeBaseDate value, string duration, int direction)
    {
        var match = Duration.Match(duration);
        if (!match.Success)
        {
            throw new InvalidDataException("Use a supported date duration, such as 7d or 1 month.");
        }
        var amount =
            NativeBaseValues.Number(double.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture))
            * direction;
        var date = value.Local;
        try
        {
            return AddUnit(date, amount, match.Groups["unit"].Value);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Date arithmetic is outside its supported range.", exception);
        }
    }

    private static NativeBaseDate AddUnit(DateTimeOffset date, double amount, string unit)
    {
        var local = date.LocalDateTime;
        var calendar = unit switch
        {
            "y" or "year" or "years" => local.AddMonths(CalendarAmount(amount * 12)),
            "M" or "month" or "months" => local.AddMonths(CalendarAmount(amount)),
            "w" or "week" or "weeks" => local.AddDays(CalendarAmount(amount * 7)),
            "d" or "day" or "days" => local.AddDays(CalendarAmount(amount)),
            _ => (DateTime?)null,
        };
        if (calendar is not null)
        {
            return Create(new DateTimeOffset(calendar.Value).ToUnixTimeMilliseconds());
        }
        var elapsed = unit switch
        {
            "h" or "hour" or "hours" => date.AddHours(amount),
            "m" or "minute" or "minutes" => date.AddMinutes(amount),
            _ => date.AddSeconds(amount),
        };
        return Create(elapsed.ToUnixTimeMilliseconds());
    }

    public static NativeBaseDate StartOfDay(NativeBaseDate date) =>
        Create(new DateTimeOffset(date.Local.LocalDateTime.Date).ToUnixTimeMilliseconds());

    public static string Format(NativeBaseDate value, string pattern) =>
        Format(value, NativeBaseValues.Text(pattern), 0);

    private static string Format(NativeBaseDate value, string pattern, int depth)
    {
        if (depth > 2)
        {
            throw new InvalidDataException("Invalid localized date format.");
        }
        var result = new StringBuilder();
        var position = 0;
        while (position < pattern.Length)
        {
            if (AppendLiteral(pattern, ref position, result))
            {
                continue;
            }
            var localized = Localized.FirstOrDefault(pair =>
                pattern.AsSpan(position).StartsWith(pair.Key, StringComparison.Ordinal)
            );
            if (localized.Key is not null)
            {
                result.Append(Format(value, localized.Value, depth + 1));
                position += localized.Key.Length;
            }
            else
            {
                AppendToken(value, pattern, ref position, result);
            }
            if (result.Length > 65_536)
            {
                throw new InvalidDataException("A formula string exceeds 65,536 characters.");
            }
        }
        return result.ToString();
    }

    private static bool AppendLiteral(string pattern, ref int position, StringBuilder result)
    {
        if (pattern[position] == '\\' && position + 1 < pattern.Length)
        {
            result.Append(pattern[position + 1]);
            position += 2;
            return true;
        }
        if (pattern[position] != '[')
        {
            return false;
        }
        var end = pattern.IndexOf(']', position + 1);
        if (end < 0)
        {
            return false;
        }
        result.Append(pattern.AsSpan(position + 1, end - position - 1));
        position = end + 1;
        return true;
    }

    private static void AppendToken(NativeBaseDate value, string pattern, ref int position, StringBuilder result)
    {
        var offset = position;
        var token = Tokens.FirstOrDefault(candidate =>
            pattern.AsSpan(offset).StartsWith(candidate, StringComparison.Ordinal)
        );
        if (token is null)
        {
            result.Append(pattern[position++]);
            return;
        }
        result.Append(Token(value, token));
        position += token.Length;
    }

    private static string Token(NativeBaseDate value, string token)
    {
        var date = value.Local;
        return token switch
        {
            "YYYY" or "Y" => date.ToString("yyyy", English),
            "YY" => date.ToString("yy", English),
            "MMMM" => date.ToString("MMMM", English),
            "MMM" => date.ToString("MMM", English),
            "dddd" => date.ToString("dddd", English),
            "ddd" => date.ToString("ddd", English),
            "MM" => Digits(date.Month, 2),
            "M" => Digits(date.Month),
            "DD" => Digits(date.Day, 2),
            "D" => Digits(date.Day),
            "Do" => Ordinal(date.Day),
            "HH" => Digits(date.Hour, 2),
            "H" => Digits(date.Hour),
            "hh" => Digits(Hour(date.Hour), 2),
            "h" => Digits(Hour(date.Hour)),
            "mm" => Digits(date.Minute, 2),
            "m" => Digits(date.Minute),
            "ss" => Digits(date.Second, 2),
            "s" => Digits(date.Second),
            "SSSS" => Digits(date.Millisecond, 3) + "0",
            "SSS" => Digits(date.Millisecond, 3),
            "SS" => Digits(date.Millisecond / 10, 2),
            "S" => Digits(date.Millisecond / 100),
            "A" => date.Hour < 12 ? "AM" : "PM",
            "a" => date.Hour < 12 ? "am" : "pm",
            "Z" => Offset(date.Offset, true),
            "ZZ" => Offset(date.Offset, false),
            "X" => Math.Floor(value.Milliseconds / 1000d).ToString(CultureInfo.InvariantCulture),
            "x" => value.Milliseconds.ToString(CultureInfo.InvariantCulture),
            "d" => Digits((int)date.DayOfWeek),
            "E" => Digits(date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek),
            "W" => Digits(ISOWeek.GetWeekOfYear(date.Date)),
            "WW" => Digits(ISOWeek.GetWeekOfYear(date.Date), 2),
            "Q" => Digits((date.Month + 2) / 3),
            "Qo" => Ordinal((date.Month + 2) / 3),
            _ => "",
        };
    }

    private static string Offset(TimeSpan value, bool colon) =>
        (value < TimeSpan.Zero ? "-" : "+")
        + value.Duration().ToString(colon ? @"hh\:mm" : "hhmm", CultureInfo.InvariantCulture);

    private static int Hour(int hour) => hour % 12 == 0 ? 12 : hour % 12;

    private static int CalendarAmount(double value) => checked((int)Math.Round(value, MidpointRounding.AwayFromZero));

    private static int Integer(string value) => int.Parse(value, CultureInfo.InvariantCulture);

    private static string Digits(int value, int count = 0) =>
        value.ToString(count == 0 ? "G" : "D" + count, CultureInfo.InvariantCulture);

    private static string Ordinal(int value) =>
        Digits(value)
        + (
            value % 100 is 11 or 12 or 13
                ? "th"
                : (value % 10) switch
                {
                    1 => "st",
                    2 => "nd",
                    3 => "rd",
                    _ => "th",
                }
        );
}
