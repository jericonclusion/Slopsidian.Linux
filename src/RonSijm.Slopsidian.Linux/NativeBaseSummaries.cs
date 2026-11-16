namespace RonSijm.Slopsidian.Linux;

public enum NativeBaseSummaryKind
{
    Number,
    Date,
    Boolean,
    Other,
}

public static class NativeBaseSummaries
{
    private static readonly Dictionary<string, string> Universal = new(StringComparer.Ordinal)
    {
        ["Empty"] = "values.filter(value.isType(\"null\")).length",
        ["Filled"] = "values.filter(!value.isType(\"null\")).length",
        ["Unique"] = "values.unique().length",
    };
    private static readonly Dictionary<string, string> Numeric = new(StringComparer.Ordinal)
    {
        ["Average"] = "values.mean().round(2)",
        ["Min"] = "values.min()",
        ["Max"] = "values.max()",
        ["Sum"] = "values.sum().round(2)",
        ["Range"] = "values.max() - values.min()",
        ["Median"] = "values.median()",
        ["Stddev"] = "values.stddev().round(2)",
    };
    private static readonly Dictionary<string, string> Dates = new(StringComparer.Ordinal)
    {
        ["Earliest"] = "values.earliest()",
        ["Latest"] = "values.latest()",
        ["Range"] = "values.latest() - values.earliest()",
    };
    private static readonly Dictionary<string, string> Checkboxes = new(StringComparer.Ordinal)
    {
        ["Checked"] = "values.filter(value == true).length",
        ["Unchecked"] = "values.filter(value == false).length",
    };
    private static readonly HashSet<string> Names = Universal
        .Keys.Concat(Numeric.Keys)
        .Concat(Dates.Keys)
        .Concat(Checkboxes.Keys)
        .ToHashSet(StringComparer.Ordinal);

    public static NativeBaseSummaryKind Kind(object? value) =>
        value switch
        {
            NativeBaseDate => NativeBaseSummaryKind.Date,
            bool => NativeBaseSummaryKind.Boolean,
            _ when NativeBaseValues.IsNumber(value) => NativeBaseSummaryKind.Number,
            _ => NativeBaseSummaryKind.Other,
        };

    public static bool IsBuiltin(string name) => Names.Contains(name);

    public static IReadOnlyDictionary<string, string> Builtins(NativeBaseSummaryKind kind) =>
        Universal.Concat(Specific(kind)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    internal static string? Definition(NativeBaseSummaryKind kind, string name, bool empty)
    {
        if (Universal.TryGetValue(name, out var universal))
        {
            return universal;
        }
        if (Specific(kind).TryGetValue(name, out var specific))
        {
            return specific;
        }
        return empty
            ? Numeric.GetValueOrDefault(name) ?? Dates.GetValueOrDefault(name) ?? Checkboxes.GetValueOrDefault(name)
            : null;
    }

    private static Dictionary<string, string> Specific(NativeBaseSummaryKind kind) =>
        kind switch
        {
            NativeBaseSummaryKind.Number => Numeric,
            NativeBaseSummaryKind.Date => Dates,
            NativeBaseSummaryKind.Boolean => Checkboxes,
            _ => [],
        };
}
