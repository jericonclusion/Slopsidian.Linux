using RonSijm.Slopsidian.Services;
using Xunit;

namespace RonSijm.Slopsidian.Linux.Tests;

public sealed class BaseExpressionTests
{
    [Theory]
    [InlineData("1 + 2 * 3", "7")]
    [InlineData("-(2 + 3) * 4", "-20")]
    [InlineData("if(false, 1 / 0, note[\"unit-price\"] * 2)", "5")]
    [InlineData("if(true, \"yes\", missing.unsupported())", "yes")]
    [InlineData("true || missing.unsupported()", "true")]
    [InlineData("false && missing.unsupported()", "false")]
    [InlineData("[\"one\", \"two\"].contains(\"two\") && title.lower().startsWith(\"note\")", "true")]
    [InlineData("number(\"2.5\").toFixed(2) + \" dollars\"", "2.50 dollars")]
    [InlineData("number(\"0x10\") + number(\"0b11\")", "19")]
    [InlineData("number(true)", "1")]
    [InlineData("file.name", "Note.md")]
    [InlineData("file.basename", "Note")]
    [InlineData("file.asLink(\"Open\").toString()", "Open")]
    [InlineData("file.hasTag(\"book\") && file.inFolder(\"Projects\")", "true")]
    [InlineData("file.hasProperty(\"price\") && file.hasLink(\"Target\")", "true")]
    [InlineData("this.file.folder", "Projects")]
    [InlineData("link(\"/Target.md\").toString()", "/Target")]
    [InlineData("(date(\"2025-01-01\") + \"1M\").format(\"YYYY-MM-DD\")", "2025-02-01")]
    [InlineData("(date(\"2025-01-31\") + \"1 month\").format(\"YYYY-MM-DD\")", "2025-02-28")]
    [InlineData("due.format(\"[due] YYYY-MM-DD\")", "due 2025-03-04")]
    [InlineData("date(\"2025-W02-3\").format(\"YYYY-MM-DD\")", "2025-01-08")]
    [InlineData("date(\"2025-032\").format(\"YYYY-MM-DD\")", "2025-02-01")]
    [InlineData("date(\"20250108\").format(\"YYYY-MM-DD\")", "2025-01-08")]
    [InlineData("date(\"2025\").format(\"YYYY-MM-DD\")", "2025-01-01")]
    [InlineData("date(\"2025-01-01T00:00:00Z\").format(\"x\")", "1735689600000")]
    [InlineData("file.mtime > date(\"2025-01-09\")", "true")]
    [InlineData("null.isEmpty() && [].isEmpty() && [].isTruthy()", "true")]
    [InlineData("false.isType(\"boolean\") && !false.isTruthy()", "true")]
    [InlineData("[1, null, 5].mean()", "2")]
    [InlineData("[1, 3].stddev()", "1")]
    [InlineData("[2, 8, 1, 3].median()", "2.5")]
    [InlineData("[0.125, 1.111].sum().round(2)", "1.24")]
    [InlineData("[].sum()", "")]
    [InlineData("[[1], [1], [2]].unique().length", "2")]
    [InlineData("[link(\"Target.md\", \"one\"), link(\"Target.md\", \"two\")].unique().length", "1")]
    [InlineData("[\"Note10\", \"Note2\", \"Note1\"].sort().join()", "Note1,Note2,Note10")]
    [InlineData("[\"a\", null, \"b\"].join()", "a,,b")]
    [InlineData("\"abcdef\".slice(-3, -1)", "de")]
    [InlineData("[1, 2, 3].slice(-2).reverse().join(\" / \")", "3 / 2")]
    [InlineData("\"abc\".replace(\"\", \"-\")", "-a-b-c-")]
    [InlineData("\"a b\".replace(\"a\", \"$&$&\")", "aa b")]
    [InlineData("[1, 2, 3].filter(value > 1).map(value * 2 + index).reduce(acc + value, 0)", "11")]
    [InlineData("-0", "0")]
    [InlineData("1.005.toFixed(2)", "1.00")]
    [InlineData("1e-7", "1e-7")]
    [InlineData("1e-6", "0.000001")]
    [InlineData("-1.23e-6", "-0.00000123")]
    [InlineData("\"a\".replace(\"a\", \"$$\")", "$")]
    [InlineData("\"abc\".replace(\"b\", \"$`$&$'\")", "aabcc")]
    [InlineData("\"a\".replace(\"\", \"$'\")", "aa")]
    public void ReferenceExpressionsProduceTheSameVisibleValues(string expression, string expected)
    {
        Assert.Equal(expected, NativeBaseValues.Display(Evaluator().Evaluate(expression)));
    }

    [Theory]
    [InlineData("[].constructor")]
    [InlineData("note[\"__proto__\"]")]
    [InlineData("file[\"constructor\"]()")]
    [InlineData("x = 2")]
    [InlineData("require(\"node:fs\")")]
    [InlineData("globalThis.process.exit()")]
    [InlineData("new Function(\"return 1\")()")]
    [InlineData("(() => 1)()")]
    [InlineData("file.name.match(/x/)")]
    [InlineData("1 / 0")]
    [InlineData("9007199254740993")]
    [InlineData("1e999")]
    [InlineData("price * \"three\"")]
    [InlineData("true > false")]
    [InlineData("number(\"0xffffffffffffffff\")")]
    [InlineData("number(\" \")")]
    [InlineData("date(\"2025-02-30\")")]
    [InlineData("date(\"03/04/2025\")")]
    [InlineData("link(\"javascript:alert(1)\")")]
    [InlineData("link(\"file:///etc/passwd\")")]
    [InlineData("number(2).round(13)")]
    [InlineData("number(2).round(1.5)")]
    public void UnsafeInvalidOrUnsupportedExpressionsFailExplicitly(string expression)
    {
        Assert.Throws<InvalidDataException>(() => Evaluator().Evaluate(expression));
    }

    [Fact]
    public void DependentFormulasAreCachedPerEvaluationAndCyclesFailWithoutPoisoningOtherValues()
    {
        var definitions = new Dictionary<string, NativeBaseExpression>
        {
            ["doubled"] = NativeBaseExpression.Parse("price * 2"),
            ["formatted"] = NativeBaseExpression.Parse("formula.doubled.toFixed(2)"),
            ["cycle"] = NativeBaseExpression.Parse("formula.other"),
            ["other"] = NativeBaseExpression.Parse("formula.cycle"),
        };
        var evaluator = Evaluator(definitions);
        Assert.Equal("6.00", evaluator.Evaluate("formula.formatted"));
        Assert.Contains(
            "Circular",
            Assert.Throws<InvalidDataException>(() => evaluator.Evaluate("formula.cycle")).Message
        );
        Assert.Equal("6.00", evaluator.Evaluate("formula.formatted"));
    }

    [Fact]
    public void SummaryBindingsCannotReadRowPropertiesAndAreRestoredAfterSuccessOrFailure()
    {
        var evaluator = Evaluator();
        var result = evaluator.EvaluateSummary(
            NativeBaseExpression.Parse(
                "values.filter(value.isType(\"number\")).map(value * 2 + index).reduce(acc + value, 0)"
            ),
            [1d, null, 3d]
        );
        Assert.Equal(9d, result);
        Assert.Equal(
            "isolated",
            evaluator.EvaluateSummary(
                NativeBaseExpression.Parse(
                    "if(file == null && note == null && this == null, \"isolated\", \"leaked\")"
                ),
                [1d]
            )
        );
        Assert.Throws<InvalidDataException>(() =>
            evaluator.EvaluateSummary(NativeBaseExpression.Parse("values.map(value.unsupported())"), [1d])
        );
        Assert.Equal(3d, evaluator.Evaluate("price"));
    }

    [Fact]
    public void ParserWorkOutputAndEvaluationAreBounded()
    {
        Assert.Throws<InvalidDataException>(() => NativeBaseExpression.Parse(new string('a', 4097)));
        Assert.Throws<InvalidDataException>(() =>
            NativeBaseExpression.Parse(new string('(', 50) + "1" + new string(')', 50))
        );
        Assert.Throws<InvalidDataException>(() =>
            NativeBaseExpression.Parse(string.Join('+', Enumerable.Repeat("0", 600)))
        );
        Assert.Throws<InvalidDataException>(() =>
            NativeBaseExpression.Parse("[" + string.Join(',', Enumerable.Repeat("0", 129)) + "]")
        );
        Assert.Throws<InvalidDataException>(() =>
            Evaluator().Evaluate("x" + string.Concat(Enumerable.Repeat(".x", 100)))
        );
        var lowBudget = new NativeBaseEvaluator(
            File("Note.md"),
            File("Catalog.base"),
            evaluationBudget: new NativeBaseBudget(5)
        );
        Assert.Throws<InvalidDataException>(() => lowBudget.Evaluate("[1, 2, 3].map(value * 2)"));
        var huge = File("Note.md", new Dictionary<string, object?> { ["value"] = new string('x', 40_000) });
        Assert.Throws<InvalidDataException>(() =>
            new NativeBaseEvaluator(huge, File("Catalog.base")).Evaluate("value + value")
        );
    }

    [Fact]
    public void FormulaDependenciesShareTheRecursionLimit()
    {
        var definitions = Enumerable
            .Range(0, 128)
            .ToDictionary(
                index => "f" + index,
                index => NativeBaseExpression.Parse(index == 127 ? "1" : "formula.f" + (index + 1))
            );
        Assert.Contains(
            "depth",
            Assert.Throws<InvalidDataException>(() => Evaluator(definitions).Evaluate("formula.f0")).Message
        );
    }

    private static NativeBaseEvaluator Evaluator(IReadOnlyDictionary<string, NativeBaseExpression>? definitions = null)
    {
        var current = File(
            "Projects/Note.md",
            new Dictionary<string, object?>
            {
                ["price"] = 3d,
                ["unit-price"] = 2.5d,
                ["title"] = "NOTE title",
                ["due"] = "2025-03-04",
            }
        ) with
        {
            PropertyTypes = new Dictionary<string, string> { ["due"] = "date" },
        };
        var target = File("Projects/Target.md");
        var files = new Dictionary<string, NativeBaseFile> { [current.Path] = current, [target.Path] = target };
        return new NativeBaseEvaluator(
            current,
            File("Projects/Catalog.base"),
            definitions,
            files,
            currentTime: DateTimeOffset
                .Parse("2025-01-11T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture)
                .ToUnixTimeMilliseconds()
        );
    }

    private static NativeBaseFile File(string path, IReadOnlyDictionary<string, object?>? properties = null)
    {
        var stamp = DateTimeOffset
            .Parse("2025-01-10T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture)
            .ToUnixTimeMilliseconds();
        return new NativeBaseFile(
            new VaultFile(
                path,
                Path.GetFileName(path),
                Path.GetFileNameWithoutExtension(path),
                Path.GetExtension(path).TrimStart('.'),
                new VaultFileStat(stamp, stamp, 123)
            ),
            properties ?? new Dictionary<string, object?>(),
            ["#book/sub"],
            ["Projects/Target.md"]
        );
    }
}
