namespace RonSijm.Slopsidian.Linux;

public enum NativeBasePropertyKind
{
    Note,
    File,
    Formula,
}

public sealed record NativeBaseProperty(NativeBasePropertyKind Kind, string Name)
{
    public string Canonical => Kind.ToString().ToLowerInvariant() + "." + Name;
}

public sealed record NativeBaseSort(string Property, string Direction = "ASC");

public abstract record NativeBaseFilter
{
    public sealed record Expression(NativeBaseExpression Syntax) : NativeBaseFilter
    {
        internal override bool Evaluate(NativeBaseEvaluator evaluator, NativeBaseBudget budget, int depth) =>
            NativeBaseValues.Truth(evaluator.Evaluate(Syntax));
    }

    public sealed record Group(string Mode, IReadOnlyList<NativeBaseFilter> Children) : NativeBaseFilter
    {
        internal override bool Evaluate(NativeBaseEvaluator evaluator, NativeBaseBudget budget, int depth) =>
            Mode switch
            {
                "and" => Children.All(child => child.Matches(evaluator, budget, depth + 1)),
                "or" => Children.Any(child => child.Matches(evaluator, budget, depth + 1)),
                "not" => !Children.Any(child => child.Matches(evaluator, budget, depth + 1)),
                _ => throw new InvalidDataException("Unsupported Bases filter."),
            };
    }

    internal bool Matches(NativeBaseEvaluator evaluator, NativeBaseBudget budget, int depth = 0)
    {
        budget.Spend();
        if (depth > 24)
        {
            throw new InvalidDataException("Filter nesting exceeds 24 levels.");
        }
        return Evaluate(evaluator, budget, depth);
    }

    internal abstract bool Evaluate(NativeBaseEvaluator evaluator, NativeBaseBudget budget, int depth);
}

public sealed record NativeBaseView(int Index, string Name, string Type, IReadOnlyList<string> Order)
{
    public NativeBaseFilter? Filter { get; init; }
    public IReadOnlyList<NativeBaseSort> Sort { get; init; } = [];
    public NativeBaseSort? GroupBy { get; init; }
    public int? Limit { get; init; }
    public IReadOnlyDictionary<string, string> Summaries { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed partial class NativeBaseDocument
{
    private readonly NativeYamlSource yaml;
    private readonly Dictionary<string, string> labels = new(StringComparer.Ordinal);
    public string Source => yaml.Source;
    public NativeBaseFilter? Filter { get; }
    public IReadOnlyList<NativeBaseView> Views { get; }
    public IReadOnlyDictionary<string, NativeBaseExpression> Formulas { get; }
    public IReadOnlyDictionary<string, string> FormulaErrors { get; }
    public IReadOnlyDictionary<string, NativeBaseExpression> SummaryFormulas { get; }
    public IReadOnlyDictionary<string, string> SummaryErrors { get; }

    public NativeBaseDocument(string source)
    {
        yaml = new NativeYamlSource(source);
        var root = Mapping(yaml.Root, "A base must be a YAML mapping containing views.");
        Filter = ReadFilter(root.Field("filters"));
        (Formulas, FormulaErrors) = Definitions(root.Field("formulas"), false);
        (SummaryFormulas, SummaryErrors) = Definitions(root.Field("summaries"), true);
        ReadLabels(root.Field("properties"));
        Views = ReadViews(root.Field("views"));
    }

    public NativeBaseView View(string name) => Views.FirstOrDefault(view => view.Name == name) ?? Views[0];

    public string Label(string property) =>
        labels.GetValueOrDefault(Property(property).Canonical) ?? Property(property).Name;

    public string? Summary(string view, string property) =>
        View(view).Summaries.GetValueOrDefault(Property(property).Canonical);

    public static NativeBaseProperty Property(string value)
    {
        var separator = value.IndexOf('.');
        var prefix = separator < 0 ? "" : value[..separator];
        var kind = prefix switch
        {
            "file" => NativeBasePropertyKind.File,
            "formula" => NativeBasePropertyKind.Formula,
            _ => NativeBasePropertyKind.Note,
        };
        var name = prefix is "file" or "note" or "formula" ? value[(separator + 1)..] : value;
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || name.Any(char.IsControl))
        {
            throw new InvalidDataException("Unsupported Bases property name: " + value);
        }
        NativeBaseValues.CheckName(name);
        return new NativeBaseProperty(kind, name);
    }

    private static List<NativeBaseView> ReadViews(NativeYamlNode? source)
    {
        if (IsNull(source))
        {
            return [new NativeBaseView(0, "Table", "table", ["file.name"])];
        }
        var items = Sequence(source!, "A base needs between 1 and 64 named views.");
        if (items.Count is < 1 or > 64)
        {
            throw new InvalidDataException("A base needs between 1 and 64 named views.");
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<NativeBaseView>();
        for (var index = 0; index < items.Count; index++)
        {
            var view = ReadView(items[index], index);
            if (!names.Add(view.Name))
            {
                throw new InvalidDataException("Base view names must be unique: " + view.Name);
            }
            result.Add(view);
        }
        return result;
    }

    private static NativeBaseView ReadView(NativeYamlNode source, int index)
    {
        var view = Mapping(source, "Every base view must be a YAML mapping.");
        var name = Text(view.Field("name"), "Every base view needs a name.");
        var type = Text(view.Field("type"), "Every base view needs a type.");
        return new NativeBaseView(index, name, type, ReadOrder(view.Field("order")))
        {
            Filter = ReadFilter(view.Field("filters")),
            Sort = ReadSort(view.Field("sort")),
            GroupBy = IsNull(view.Field("groupBy")) ? null : SortedProperty(view.Field("groupBy")!),
            Limit = ReadLimit(view.Field("limit")),
            Summaries = ReadSummaries(view.Field("summaries")),
            Warnings = view.Fields.ContainsKey("groupOrder")
                ? ["Explicit group ordering/visibility is retained but not implemented."]
                : [],
        };
    }

    private static string[] ReadOrder(NativeYamlNode? source)
    {
        if (IsNull(source))
        {
            return ["file.name"];
        }
        var items = Sequence(source!, "A table needs between 1 and 64 property columns.");
        if (items.Count is < 1 or > 64)
        {
            throw new InvalidDataException("A table needs between 1 and 64 property columns.");
        }
        var result = items.Select(node => Text(node, "Column names must be strings.")).ToArray();
        if (result.Select(key => Property(key).Canonical).Distinct(StringComparer.Ordinal).Count() != result.Length)
        {
            throw new InvalidDataException("A table cannot contain duplicate property columns.");
        }
        return result;
    }

    private static NativeBaseSort[] ReadSort(NativeYamlNode? source)
    {
        if (IsNull(source))
        {
            return [];
        }
        var items = Sequence(source!, "A table supports at most 16 sort properties.");
        if (items.Count > 16)
        {
            throw new InvalidDataException("A table supports at most 16 sort properties.");
        }
        return items.Select(SortedProperty).ToArray();
    }

    private static NativeBaseSort SortedProperty(NativeYamlNode source)
    {
        var mapping = Mapping(source, "Sorting/grouping needs a property and direction.");
        var property = Text(mapping.Field("property"), "Choose a property for sorting/grouping.");
        _ = Property(property);
        var direction = IsNull(mapping.Field("direction"))
            ? "ASC"
            : Text(mapping.Field("direction"), "Sort directions must be ASC or DESC.");
        if (direction is not ("ASC" or "DESC"))
        {
            throw new InvalidDataException("Sort directions must be ASC or DESC.");
        }
        return new NativeBaseSort(property, direction);
    }

    private static int? ReadLimit(NativeYamlNode? source)
    {
        if (IsNull(source))
        {
            return null;
        }
        if (source!.Resolved.Kind != NativeYamlKind.Scalar || source.Resolved.Text.Length > 64)
        {
            throw new InvalidDataException("A view limit must be an integer from 0 to 10,000.");
        }
        var value = NativeBaseValues.Number(NativeYamlScalar.Value(source));
        if (!double.IsInteger(value) || value is < 0 or > 10_000)
        {
            throw new InvalidDataException("A view limit must be an integer from 0 to 10,000.");
        }
        return (int)value;
    }

    private static Dictionary<string, string> ReadSummaries(NativeYamlNode? source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (IsNull(source))
        {
            return result;
        }
        var mapping = Mapping(source!, "View summaries must map properties to summary names.");
        if (mapping.Fields.Count > 128)
        {
            throw new InvalidDataException("A view supports at most 128 summary assignments.");
        }
        foreach (var pair in mapping.Fields)
        {
            var property = Property(pair.Key).Canonical;
            var name = SummaryName(Text(pair.Value.Value, "Summary names must be strings."));
            if (!result.TryAdd(property, name))
            {
                throw new InvalidDataException("A view cannot contain duplicate summary properties: " + property);
            }
        }
        return result;
    }

    private static NativeBaseFilter? ReadFilter(NativeYamlNode? source) =>
        ReadFilter(source, 0, new NativeBaseBudget(10_000));

    private static NativeBaseFilter? ReadFilter(NativeYamlNode? source, int depth, NativeBaseBudget parseBudget)
    {
        if (parseBudget.Remaining == 0)
        {
            throw new InvalidDataException("Base filter alias expansion exceeds 10,000 conditions.");
        }
        parseBudget.Spend();
        if (IsNull(source))
        {
            return null;
        }
        if (depth > 24)
        {
            throw new InvalidDataException("Filter nesting exceeds 24 levels.");
        }
        var value = source!.Resolved;
        if (value.Kind == NativeYamlKind.Scalar)
        {
            return new NativeBaseFilter.Expression(
                NativeBaseExpression.Parse(Text(value, "Filters must be expressions or and/or/not mappings."))
            );
        }
        var mapping = Mapping(value, "Filters must be expressions or and/or/not mappings.");
        if (mapping.Fields.Count != 1)
        {
            throw new InvalidDataException("A filter mapping needs exactly one and/or/not key.");
        }
        var entry = mapping.Fields.Single();
        if (entry.Key is not ("and" or "or" or "not"))
        {
            throw new InvalidDataException("A filter mapping needs exactly one and/or/not key.");
        }
        var items = Sequence(entry.Value.Value, "Filter groups need at most 128 conditions.");
        if (items.Count > 128)
        {
            throw new InvalidDataException("Filter groups need at most 128 conditions.");
        }
        return new NativeBaseFilter.Group(
            entry.Key,
            items
                .Select(child =>
                    ReadFilter(child, depth + 1, parseBudget)
                    ?? throw new InvalidDataException("A filter condition cannot be null.")
                )
                .ToArray()
        );
    }

    private static (
        Dictionary<string, NativeBaseExpression> Definitions,
        Dictionary<string, string> Errors
    ) Definitions(NativeYamlNode? source, bool summary)
    {
        var definitions = new Dictionary<string, NativeBaseExpression>(StringComparer.Ordinal);
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (IsNull(source))
        {
            return (definitions, errors);
        }
        var mapping = Mapping(source!, "Formulas and custom summaries must map names to expression strings.");
        if (mapping.Fields.Count > 128)
        {
            throw new InvalidDataException("A base supports at most 128 formulas or custom summaries.");
        }
        foreach (var entry in mapping.Fields)
        {
            if (summary)
            {
                SummaryName(entry.Key);
            }
            else
            {
                _ = Property("formula." + entry.Key);
            }
            try
            {
                definitions.Add(
                    entry.Key,
                    NativeBaseExpression.Parse(
                        Text(entry.Value.Value, "Formula definitions must be expression strings.")
                    )
                );
            }
            catch (InvalidDataException exception)
            {
                errors.Add(entry.Key, exception.Message);
            }
        }
        return (definitions, errors);
    }

    private void ReadLabels(NativeYamlNode? source)
    {
        if (IsNull(source))
        {
            return;
        }
        var mapping = Mapping(source!, "Property configuration must be a mapping.");
        foreach (var entry in mapping.Fields)
        {
            var configuration = Mapping(entry.Value.Value, "Property display configuration must be a mapping.");
            if (!IsNull(configuration.Field("displayName")))
            {
                labels[Property(entry.Key).Canonical] = Text(
                    configuration.Field("displayName"),
                    "Property display names must be strings."
                );
            }
        }
    }

    internal static string SummaryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.Any(char.IsControl))
        {
            throw new InvalidDataException("A summary name needs 1–128 characters without control or reserved names.");
        }
        NativeBaseValues.CheckName(name);
        return name;
    }

    private static NativeYamlNode Mapping(NativeYamlNode source, string message) =>
        source.Resolved.Kind == NativeYamlKind.Mapping ? source.Resolved : throw new InvalidDataException(message);

    private static List<NativeYamlNode> Sequence(NativeYamlNode source, string message) =>
        source.Resolved.Kind == NativeYamlKind.Sequence
            ? source.Resolved.Items
            : throw new InvalidDataException(message);

    private static bool IsNull(NativeYamlNode? source) =>
        source is null || source.Resolved.Kind == NativeYamlKind.Scalar && NativeYamlScalar.Value(source) is null;

    private static string Text(NativeYamlNode? source, string message)
    {
        if (
            source is null
            || source.Resolved.Kind != NativeYamlKind.Scalar
            || NativeYamlScalar.Value(source) is not string text
            || string.IsNullOrWhiteSpace(text)
            || text.Length > 4096
            || text.Contains('\0')
        )
        {
            throw new InvalidDataException(message);
        }
        return text;
    }
}
