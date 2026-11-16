using System.Globalization;
using RonSijm.Slopsidian.Services;
using Xunit;

namespace RonSijm.Slopsidian.Linux.Tests;

public sealed class BaseQueryTests
{
    [Fact]
    public void GlobalAndViewFiltersUseRealTagsFoldersLinksAndNorSemantics()
    {
        var files = new[]
        {
            File("Projects/A.md", 5d, "open") with
            {
                Tags = ["#book/sub"],
                Links = ["Projects/Target.md"],
            },
            File("Projects/B.md", 8d, "done") with
            {
                Tags = ["#book"],
            },
            File("Archive/C.md", 9d, "open") with
            {
                Tags = ["#book"],
            },
            File("Projects/D.md", 3d, "open") with
            {
                Tags = ["#skip"],
            },
            File("Image.png"),
        };
        const string source =
            "filters:\n  and:\n    - file.ext == \"md\"\n    - file.hasTag(\"book\")\n    - not:\n        - status == \"done\"\n        - file.inFolder(\"Archive\")\nformulas:\n  total: amount * 2\nviews:\n  - type: table\n    name: Table\n    order: [file.name, status, amount, formula.total]\n    filters:\n      or:\n        - amount >= 4\n        - file.hasLink(\"Target\")\n";
        var result = Query(source, files);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Projects/A.md", row.Path);
        Assert.Equal(new[] { "A.md", "open", "5", "10" }, row.Cells.Select(cell => cell.Text));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SortingGroupingLimitsLabelsSearchAndPagesUseEvaluatedValues()
    {
        var files = Enumerable
            .Range(0, 130)
            .Select(index => File("Note " + index + ".md", (double)index, index % 2 == 1 ? "open" : "done"))
            .ToArray();
        const string source =
            "formulas: {total: 'amount * 2'}\nproperties: {formula.total: {displayName: Amount}}\nviews:\n  - type: table\n    name: Table\n    order: [file.name, status, amount, formula.total]\n    sort: [{property: amount, direction: DESC}]\n    groupBy: {property: status, direction: ASC}\n    limit: 120\n";
        var first = Query(source, files);
        var second = Query(source, files, page: 1);
        Assert.Equal(120, first.Total);
        Assert.Equal(130, first.Matched);
        Assert.Equal(100, first.Rows.Count);
        Assert.Equal(20, second.Rows.Count);
        Assert.Equal("Amount", first.Columns[3].Label);
        Assert.Equal("Note 128.md", first.Rows[0].Path);
        Assert.Equal(new[] { ("done", 65), ("open", 55) }, first.Groups.Select(group => (group.Label, group.Count)));
        Assert.Equal("Note 129.md", Assert.Single(Query(source, files, search: "Note 129.md").Rows).Path);
        Assert.Equal(1, Query(source, files, page: int.MaxValue).Page);
    }

    [Fact]
    public void WholeResultAndGroupSummariesIncludeOffPageRowsAndFollowSearchFiltersAndLimits()
    {
        var files = Enumerable
            .Range(1, 130)
            .Select(index => File("Note " + index + ".md", (double)index, index % 2 == 0 ? "open" : "done"))
            .ToArray();
        const string source =
            "views:\n  - type: table\n    name: Table\n    order: [file.name, amount]\n    groupBy: {property: status, direction: ASC}\n    summaries: {amount: Sum}\n";
        var first = Query(source, files);
        var second = Query(source, files, page: 1);
        Assert.Equal(100, first.Rows.Count);
        Assert.Equal(30, second.Rows.Count);
        Assert.Equal("8515", first.Summaries[1]!.Text);
        Assert.Equal("8515", second.Summaries[1]!.Text);
        Assert.Equal(
            new[] { ("done", 65, "4225"), ("open", 65, "4290") },
            first.Groups.Select(group => (group.Label, group.Count, group.Summaries![1]!.Text))
        );
        Assert.Equal("4290", second.Groups.Single(group => group.Label == "open").Summaries![1]!.Text);
        Assert.Equal("130", Query(source, files, search: "Note 130.md").Summaries[1]!.Text);
        Assert.Equal("4255", Query(source + "    limit: 70\n", files).Summaries[1]!.Text);
        Assert.Equal("4290", Query("filters: status == \"open\"\n" + source, files).Summaries[1]!.Text);
        files[129] = File("Note 130.md", 9_007_199_254_740_992d, "open");
        var broken = Query(source, files);
        Assert.DoesNotContain(broken.Rows, row => row.Path == "Note 130.md");
        Assert.Equal("#ERROR", broken.Summaries[1]!.Text);
        Assert.Contains("cell errors", broken.Summaries[1]!.Error!, StringComparison.Ordinal);
        Assert.Equal("4225", broken.Groups[0].Summaries![1]!.Text);
    }

    [Theory]
    [InlineData("Average", "5")]
    [InlineData("Min", "1")]
    [InlineData("Max", "9")]
    [InlineData("Sum", "15")]
    [InlineData("Range", "8")]
    [InlineData("Median", "5")]
    [InlineData("Stddev", "3.27")]
    public void NumericSummariesMatchThePinnedReferenceFormulas(string name, string expected)
    {
        Assert.Equal(expected, Summary(name, [File("A.md", 1d), File("B.md", 5d), File("C.md", 9d)]).Text);
    }

    [Fact]
    public void MeanDenominatorAndNullTypeDistinctionsMatchTheReference()
    {
        Assert.Equal("2", Summary("Average", [File("A.md", 1d), File("B.md"), File("C.md", 5d)]).Text);
        var files = new object?[] { true, false, null, "", Array.Empty<object?>(), 0d }
            .Select((amount, index) => File(index + ".md", amount))
            .ToArray();
        Assert.Equal("1", Summary("Checked", files).Text);
        Assert.Equal("1", Summary("Unchecked", files).Text);
        Assert.Equal("1", Summary("Empty", files).Text);
        Assert.Equal("5", Summary("Filled", files).Text);
        Assert.Equal("6", Summary("Unique", files).Text);
    }

    [Fact]
    public void TypedDateSummariesRetainRangesAndLinkSummariesUsePathIdentity()
    {
        var files = new[] { File("A.md", "2025-03-01"), File("B.md", "2025-03-03"), File("C.md") }
            .Select(file => file with { PropertyTypes = new Dictionary<string, string> { ["amount"] = "date" } })
            .ToArray();
        Assert.StartsWith("2025-03-01", Summary("Earliest", files).Text, StringComparison.Ordinal);
        Assert.StartsWith("2025-03-03", Summary("Latest", files).Text, StringComparison.Ordinal);
        Assert.Equal("172800000", Summary("Range", files).Text);
        const string source =
            "formulas: {target: 'link(\"Target.md\", file.name)'}\nviews: [{type: table, name: Table, order: [formula.target], summaries: {formula.target: Unique}}]\n";
        Assert.Equal("1", Query(source, [File("A.md"), File("B.md")]).Summaries[0]!.Text);
    }

    [Theory]
    [InlineData("Average")]
    [InlineData("Min")]
    [InlineData("Max")]
    [InlineData("Median")]
    [InlineData("Stddev")]
    [InlineData("Sum")]
    [InlineData("Range")]
    [InlineData("Earliest")]
    [InlineData("Latest")]
    public void EmptyAndAllNullColumnsKeepExplicitSummaryChoices(string name)
    {
        Assert.Equal("No value", Summary(name, []).Text);
        Assert.Equal("No value", Summary(name, [File("A.md"), File("B.md")]).Text);
    }

    [Fact]
    public void MissingGroupValuesStayDistinctFromALiteralNoValueLabelAndStructuredGroupsFail()
    {
        const string source =
            "views: [{type: table, name: Table, order: [file.name, status], groupBy: {property: status}}]\n";
        var result = Query(source, [File("A.md", status: "No value"), File("B.md")]);
        Assert.Equal(2, result.Groups.Count);
        Assert.NotEqual(result.Rows[0].GroupKey, result.Rows[1].GroupKey);
        var structured = File("A.md") with
        {
            Properties = new Dictionary<string, object?> { ["status"] = new object?[] { "one", "two" } },
        };
        Assert.Throws<InvalidDataException>(() => Query(source, [structured]));
    }

    [Fact]
    public void CustomSummariesAreIsolatedAndInvalidSummaryDoesNotEraseOtherColumns()
    {
        const string source =
            "summaries:\n  precise: values.mean().round(3)\n  doubled: values.filter(value.isType(\"number\")).map(value * 2 + index).reduce(acc + value, 0)\n  isolated: if(file == null && note == null && this == null, \"isolated\", \"leaked\")\nviews: [{type: table, name: Table, order: [amount, note.done, file.name], summaries: {amount: precise, note.done: doubled, file.name: isolated}}]\n";
        var files = new[] { File("A.md", 1d), File("B.md", 3d) }
            .Select(file =>
                file with
                {
                    Properties = new Dictionary<string, object?>(file.Properties)
                    {
                        ["done"] = file.Properties["amount"],
                    },
                }
            )
            .ToArray();
        var result = Query(source, files);
        Assert.Equal(new[] { "2", "9", "isolated" }, result.Summaries.Select(cell => cell!.Text));
        var broken = Query(
            "summaries: {broken: 'values.'}\nviews: [{type: table, name: Table, order: [file.name, amount], summaries: {file.name: Filled, amount: broken}}]\n",
            files
        );
        Assert.Equal("2", broken.Summaries[0]!.Text);
        Assert.Equal("#ERROR", broken.Summaries[1]!.Text);
        Assert.NotEmpty(broken.Errors);
    }

    [Fact]
    public void FormulaAndFilterFailuresRemainVisibleAndAreCapped()
    {
        const string source =
            "formulas: {total: 'formula.other', other: 'formula.total'}\nviews: [{type: table, name: Table, order: [file.name, formula.total]}]\n";
        var result = Query(source, [File("A.md")]);
        Assert.Equal("#ERROR", result.Rows[0].Cells[1].Text);
        Assert.Contains("Circular", result.Rows[0].Cells[1].Error!, StringComparison.Ordinal);
        var invalid = Query(
            "filters: amount * \"bad\"\nviews: [{type: table, name: Table}]\n",
            Enumerable.Range(0, 100).Select(index => File(index + ".md", 3d)).ToArray()
        );
        Assert.Empty(invalid.Rows);
        Assert.Equal(50, invalid.Errors.Count);
    }

    [Fact]
    public void KnownFileLinksKeepExactPathsWhileExpressionLinksRetainReferenceResolution()
    {
        const string source =
            "formulas: {exact: 'file.asLink()', reference: 'link(\"Note.md\")'}\nviews: [{type: table, name: Table, order: [file.name, formula.exact, formula.reference]}]\n";
        var cells = Assert.Single(Query(source, [File("Note.md")]).Rows).Cells;
        Assert.True(cells[0].Link!.ExactPath);
        Assert.True(cells[1].Link!.ExactPath);
        Assert.False(cells[2].Link!.ExactPath);
        Assert.Equal("Note.md", cells[0].Link!.Path);
    }

    [Fact]
    public void CancellationFileLimitsAndUnsupportedViewsFailBeforeReturningPartialResults()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var request = new NativeBaseQueryRequest(
            new NativeBaseDocument("views: [{type: table, name: Table}]"),
            [],
            File("Catalog.base")
        )
        {
            CancellationToken = cancellation.Token,
        };
        Assert.Throws<OperationCanceledException>(() => NativeBaseQuery.Execute(request));
        Assert.Throws<InvalidDataException>(() =>
            Query("views: [{type: table, name: Table}]", Enumerable.Repeat(File("A.md"), 10_001).ToArray())
        );
        Assert.Contains(
            "retained but not implemented",
            Assert.Throws<InvalidDataException>(() => Query("views: [{type: cards, name: Gallery}]", [])).Message,
            StringComparison.Ordinal
        );
    }

    private static NativeBaseSummaryCell Summary(string name, NativeBaseFile[] files) =>
        Query(
            "views: [{type: table, name: Table, order: [file.name, amount], summaries: {amount: '" + name + "'}}]\n",
            files
        ).Summaries[1]!;

    private static NativeBaseResult Query(string source, NativeBaseFile[] files, string search = "", int page = 0) =>
        NativeBaseQuery.Execute(
            new NativeBaseQueryRequest(new NativeBaseDocument(source), files, File("Catalog.base"))
            {
                Search = search,
                Page = page,
            }
        );

    private static NativeBaseFile File(string path, object? amount = null, string? status = null)
    {
        var stamp = DateTimeOffset.Parse("2025-01-10T12:00:00Z", CultureInfo.InvariantCulture).ToUnixTimeMilliseconds();
        var properties = new Dictionary<string, object?>();
        if (amount is not null)
        {
            properties["amount"] = amount;
        }
        if (status is not null)
        {
            properties["status"] = status;
        }
        return new NativeBaseFile(
            new VaultFile(
                path,
                Path.GetFileName(path),
                Path.GetFileNameWithoutExtension(path),
                Path.GetExtension(path).TrimStart('.'),
                new VaultFileStat(stamp, stamp, 123)
            ),
            properties,
            [],
            []
        );
    }
}
