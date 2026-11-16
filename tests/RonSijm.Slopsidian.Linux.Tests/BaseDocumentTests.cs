using Xunit;

namespace RonSijm.Slopsidian.Linux.Tests;

public sealed class BaseDocumentTests
{
    private const string Source =
        "\ufeff# Existing base\r\nfuture: &future\r\n  integer: 9007199254740993\r\n  decimal: 0.123456789012345678901\r\ncopy: *future\r\nproperties:\r\n  status:\r\n    displayName: Progress\r\nviews:\r\n  - type: table\r\n    name: Table\r\n    futureView: {keep: true}\r\n    order: [file.name, note.status]\r\n";

    [Fact]
    public void SortingAndGroupingRetainOpaqueYamlCommentsAliasesPreciseNumbersBomAndLineEndings()
    {
        var original = new NativeBaseDocument(Source);
        Assert.Equal(Source, original.Source);
        Assert.Equal("Progress", original.Label("note.status"));
        var changed = original.SetView("Table", "sort", new[] { new NativeBaseSort("note.status", "DESC") });
        foreach (
            var fragment in new[]
            {
                "# Existing base",
                "&future",
                "*future",
                "integer: 9007199254740993",
                "decimal: 0.123456789012345678901",
                "futureView: {keep: true}",
            }
        )
        {
            Assert.Contains(fragment, changed, StringComparison.Ordinal);
        }
        Assert.StartsWith("\ufeff", changed, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', changed.Replace("\r\n", "", StringComparison.Ordinal));
        var sorted = new NativeBaseDocument(changed);
        Assert.Equal(new NativeBaseSort("note.status", "DESC"), Assert.Single(sorted.View("Table").Sort));
        var grouped = new NativeBaseDocument(sorted.SetView("Table", "groupBy", new NativeBaseSort("note.status")));
        Assert.Equal(new NativeBaseSort("note.status"), grouped.View("Table").GroupBy);
        Assert.Null(new NativeBaseDocument(grouped.SetView("Table", "groupBy", null)).View("Table").GroupBy);
        Assert.Equal(Source, original.Source);
    }

    [Theory]
    [InlineData("views: [{type: table, name: Table, order: [file.name], future: {integer: 9007199254740993}}]")]
    [InlineData(
        "# emoji 🐧 stays intact\nviews:\n  - type: table\n    name: Table\n    order: [file.name] # Keep column comment\nfuture: 0.123456789012345678901\n"
    )]
    [InlineData(
        "views:\n  - name: Table\n    type: table\n    order: [file.name]\n  - name: Other\n    type: table\n    order: [file.path]\n"
    )]
    [InlineData("future: {keep: true}\n")]
    public void ViewChangesRemainValidAcrossFlowBlockUnicodeMultipleAndImplicitViews(string source)
    {
        var document = new NativeBaseDocument(source);
        var changed = document.SetView("Table", "groupBy", new NativeBaseSort("file.ext", "DESC"));
        var grouped = new NativeBaseDocument(changed);
        Assert.Equal(new NativeBaseSort("file.ext", "DESC"), grouped.View("Table").GroupBy);
        var removed = new NativeBaseDocument(grouped.SetView("Table", "groupBy", null));
        Assert.Null(removed.View("Table").GroupBy);
        Assert.Equal(document.Views.Count, removed.Views.Count);
        Assert.Equal(source, document.Source);
    }

    [Fact]
    public void SummaryChangesKeepUnrelatedViewsDefinitionsAndExactOpaqueData()
    {
        var source =
            "\ufeff# Keep summary source\r\nfuture: &future {integer: 9007199254740993, decimal: 0.123456789012345678901}\r\ncopy: *future\r\nsummaries:\r\n  old: values.sum() # Retain custom formula\r\nviews:\r\n  - type: table\r\n    name: Table\r\n    order: [file.name, note.amount]\r\n    futureView: {retain: true}\r\n    summaries: {amount: old, file.name: Filled}\r\n  - type: table\r\n    name: Other\r\n    summaries: {note.amount: old}\r\n";
        var document = new NativeBaseDocument(source);
        var changed = document.SetSummary("Table", "note.amount", "Sum");
        foreach (
            var fragment in new[]
            {
                "# Keep summary source",
                "&future",
                "*future",
                "9007199254740993",
                "0.123456789012345678901",
                "# Retain custom formula",
                "futureView: {retain: true}",
            }
        )
        {
            Assert.Contains(fragment, changed, StringComparison.Ordinal);
        }
        var updated = new NativeBaseDocument(changed);
        Assert.Equal("Sum", updated.Summary("Table", "amount"));
        Assert.Equal("old", updated.Summary("Other", "note.amount"));
        Assert.Equal("Filled", updated.Summary("Table", "file.name"));
        var custom = new NativeBaseDocument(
            updated.SetSummary(
                "Table",
                "amount",
                "Precise",
                new NativeBaseCustomSummary("Precise", "values.mean().round(3)")
            )
        );
        Assert.Equal("Precise", custom.Summary("Table", "amount"));
        Assert.True(custom.SummaryFormulas.ContainsKey("old"));
        Assert.True(custom.SummaryFormulas.ContainsKey("Precise"));
        var removed = new NativeBaseDocument(custom.SetSummary("Table", "amount", null));
        Assert.Null(removed.Summary("Table", "amount"));
        Assert.Equal("Filled", removed.Summary("Table", "file.name"));
        Assert.StartsWith("\ufeff", removed.Source, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', removed.Source.Replace("\r\n", "", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("views: [{type: table, name: Table, order: [amount], summaries: {amount: Sum}}]")]
    [InlineData("views:\n  - type: table\n    name: Table\n    order: [amount]\n    summaries:\n      amount: Sum")]
    [InlineData("views:\n  - summaries:\n      amount: Sum\n    type: table\n    name: Table\n    order: [amount]\n")]
    public void RemovingTheLastSummaryDoesNotLeaveAnInvalidMappingOrDamageItsSequenceItem(string source)
    {
        var updated = new NativeBaseDocument(new NativeBaseDocument(source).SetSummary("Table", "amount", null));
        Assert.Null(updated.Summary("Table", "amount"));
        Assert.Single(updated.Views);
        Assert.Equal("table", updated.View("Table").Type);
    }

    [Theory]
    [InlineData("[array]")]
    [InlineData("broken: [")]
    [InlineData("views: []")]
    [InlineData("views: [{type: table, name: A}, {type: table, name: A}]")]
    [InlineData("views: [{type: table, name: A, order: [status, note.status]}]")]
    [InlineData("filters: {unknown: []}\nviews: [{type: table, name: A}]")]
    [InlineData("views: [{type: table, name: A, limit: 10001}]")]
    [InlineData("views: [{type: table, name: A, limit: 0.5}]")]
    [InlineData("views: [{type: table, name: A, name: B}]")]
    [InlineData("views: [{type: table, name: false}]")]
    [InlineData("views: [{type: table, name: A, summaries: {status: Sum, note.status: Average}}]")]
    [InlineData("first: one\n---\nsecond: two")]
    [InlineData("unknown: *missing\nviews: [{type: table, name: Table}]")]
    [InlineData("unknown: !arbitrary value\nviews: [{type: table, name: Table}]")]
    [InlineData("future: !!map wrong\nviews: [{type: table, name: Table}]")]
    [InlineData("future: !!bool yes\nviews: [{type: table, name: Table}]")]
    [InlineData("future: !!str {wrong: mapping}\nviews: [{type: table, name: Table}]")]
    public void InvalidOrAmbiguousDocumentsFailBeforeAnyReplacementSourceExists(string source)
    {
        Assert.Throws<InvalidDataException>(() => new NativeBaseDocument(source));
    }

    [Fact]
    public void InvalidFormulasStayVisibleWithoutDiscardingTheBaseOrOtherDefinitions()
    {
        var source =
            "formulas: {bad: 'price *', good: 'price * 2'}\nsummaries: {bad: 'values.unknown(', good: 'values.sum()'}\nviews: [{type: table, name: Table}]\n";
        var document = new NativeBaseDocument(source);
        Assert.Equal(source, document.Source);
        Assert.True(document.FormulaErrors.ContainsKey("bad"));
        Assert.True(document.Formulas.ContainsKey("good"));
        Assert.True(document.SummaryErrors.ContainsKey("bad"));
        Assert.True(document.SummaryFormulas.ContainsKey("good"));
    }

    [Fact]
    public void AliasedViewsAreReadableAndEditingRefusesToDetachOrAlterTheirSharedOwner()
    {
        var source = "shared: &shared {type: table, name: Table, order: [file.name]}\nviews: [*shared]\n";
        var document = new NativeBaseDocument(source);
        Assert.Equal("Table", Assert.Single(document.Views).Name);
        Assert.Throws<InvalidDataException>(() =>
            document.SetView("Table", "sort", new[] { new NativeBaseSort("file.name") })
        );
        Assert.Equal(source, document.Source);
    }

    [Fact]
    public void InvalidEditOrCustomSummaryLeavesTheOriginalConfigurationIntact()
    {
        var document = new NativeBaseDocument("views: [{type: table, name: Table, order: [amount]}]\n");
        Assert.Throws<InvalidDataException>(() =>
            document.SetView("Table", "groupBy", new NativeBaseSort("amount", "sideways"))
        );
        Assert.Throws<InvalidDataException>(() => document.SetSummary("Table", "unknown", "Sum"));
        Assert.Throws<InvalidDataException>(() =>
            document.SetSummary("Table", "amount", "Sum", new NativeBaseCustomSummary("Sum", "values.max()"))
        );
        Assert.Throws<InvalidDataException>(() =>
            document.SetSummary("Table", "amount", "New", new NativeBaseCustomSummary("New", "values."))
        );
        Assert.Null(document.Summary("Table", "amount"));
    }

    [Fact]
    public void SharedFilterAliasesCannotExpandIntoAnUnboundedConditionTree()
    {
        var lines = new List<string> { "f0: &f0 'true'" };
        for (var index = 1; index <= 10; index++)
        {
            lines.Add(
                "f"
                    + index
                    + ": &f"
                    + index
                    + " {and: [*f"
                    + (index - 1)
                    + ", *f"
                    + (index - 1)
                    + ", *f"
                    + (index - 1)
                    + "]}"
            );
        }
        lines.Add("filters: *f10");
        Assert.Throws<InvalidDataException>(() => new NativeBaseDocument(string.Join('\n', lines)));
    }

    [Fact]
    public void UnknownViewTypesAreRetainedAndLargeOrDeepSourcesAreBounded()
    {
        var source = "future: {keep: true}\nviews: [{type: cards, name: Gallery}]\n";
        Assert.Equal(source, new NativeBaseDocument(source).Source);
        Assert.Equal("cards", new NativeBaseDocument(source).View("Gallery").Type);
        Assert.Throws<InvalidDataException>(() => new NativeBaseDocument(new string('x', 1_048_577)));
        Assert.Throws<InvalidDataException>(() =>
            new NativeBaseDocument("future: " + new string('[', 70) + "0" + new string(']', 70))
        );
    }
}
