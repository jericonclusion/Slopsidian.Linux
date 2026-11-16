using System.Text.Json;

namespace RonSijm.Slopsidian.Linux;

public sealed record NativeBaseCustomSummary(string Name, string Formula);

public sealed partial class NativeBaseDocument
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string SetView(string view, string field, object? value)
    {
        if (field is not ("sort" or "groupBy"))
        {
            throw new InvalidDataException("Edit this view field in the base source.");
        }
        var index = View(view).Index;
        var editor = MaterializedViews();
        var mapping = DirectView(editor, index);
        var result = editor.SetField(
            mapping,
            field,
            value is null ? null : JsonSerializer.Serialize(value, JsonOptions)
        );
        return Validate(result);
    }

    public string SetSummary(string view, string property, string? name, NativeBaseCustomSummary? custom = null)
    {
        var selected = View(view);
        var canonical = Property(property).Canonical;
        if (!selected.Order.Any(column => Property(column).Canonical == canonical))
        {
            throw new InvalidDataException("Choose a displayed column to summarize.");
        }
        if (name is not null)
        {
            SummaryName(name);
        }
        ValidateCustomSummary(custom, name);
        var editor = MaterializedViews();
        if (custom is not null)
        {
            editor = AddCustomSummary(editor, custom);
        }
        var result = AssignSummary(editor, selected.Index, property, canonical, name);
        return Validate(result);
    }

    private void ValidateCustomSummary(NativeBaseCustomSummary? custom, string? name)
    {
        if (custom is null)
        {
            return;
        }
        SummaryName(custom.Name);
        _ = NativeBaseExpression.Parse(custom.Formula);
        if (
            custom.Name != name
            || NativeBaseSummaries.IsBuiltin(custom.Name)
            || SummaryFormulas.ContainsKey(custom.Name)
            || SummaryErrors.ContainsKey(custom.Name)
        )
        {
            throw new InvalidDataException(
                "Choose a new custom summary name. Edit existing definitions in the base source."
            );
        }
    }

    private static NativeYamlSource AddCustomSummary(NativeYamlSource editor, NativeBaseCustomSummary custom)
    {
        var existing = editor.Root.Field("summaries");
        var result = IsNull(existing)
            ? editor.SetField(
                editor.Root,
                "summaries",
                JsonSerializer.Serialize(new Dictionary<string, string> { [custom.Name] = custom.Formula }, JsonOptions)
            )
            : editor.SetField(existing!, custom.Name, JsonSerializer.Serialize(custom.Formula));
        return new NativeYamlSource(result);
    }

    private static string AssignSummary(
        NativeYamlSource editor,
        int index,
        string property,
        string canonical,
        string? name
    )
    {
        var view = DirectView(editor, index);
        var summaries = view.Field("summaries");
        if (IsNull(summaries))
        {
            return name is null
                ? editor.Source
                : editor.SetField(
                    view,
                    "summaries",
                    JsonSerializer.Serialize(new Dictionary<string, string> { [property] = name }, JsonOptions)
                );
        }
        if (summaries!.Kind != NativeYamlKind.Mapping)
        {
            throw new InvalidDataException("Edit this aliased summary configuration in the base source.");
        }
        var key = summaries.Fields.Keys.FirstOrDefault(candidate => Property(candidate).Canonical == canonical);
        if (key is null && name is null)
        {
            return editor.Source;
        }
        if (key is not null && name is null && summaries.Fields.Count == 1)
        {
            return editor.SetField(view, "summaries", null);
        }
        return editor.SetField(summaries, key ?? property, name is null ? null : JsonSerializer.Serialize(name));
    }

    private NativeYamlSource MaterializedViews()
    {
        if (!IsNull(yaml.Root.Field("views")))
        {
            return yaml;
        }
        return new NativeYamlSource(yaml.SetField(yaml.Root, "views", "[{\"type\":\"table\",\"name\":\"Table\"}]"));
    }

    private static NativeYamlNode DirectView(NativeYamlSource source, int index)
    {
        var views = source.Root.Field("views");
        if (views?.Kind != NativeYamlKind.Sequence || views.Items[index].Kind != NativeYamlKind.Mapping)
        {
            throw new InvalidDataException("Edit this aliased view configuration in the base source.");
        }
        return views.Items[index];
    }

    private static string Validate(string source)
    {
        _ = new NativeBaseDocument(source);
        return source;
    }
}
