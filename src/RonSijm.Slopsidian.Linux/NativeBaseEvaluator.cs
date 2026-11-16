using System.Globalization;

namespace RonSijm.Slopsidian.Linux;

public sealed partial class NativeBaseEvaluator
{
    private enum ReferenceKind
    {
        Formulas,
    }

    private sealed record Binding(object? Value, int Index, object? Accumulator);

    private readonly NativeBaseFile file;
    private readonly NativeBaseFile context;
    private readonly IReadOnlyDictionary<string, NativeBaseExpression> formulas;
    private readonly IReadOnlyDictionary<string, NativeBaseFile> files;
    private readonly NativeBaseBudget budget;
    private readonly long now;
    private readonly Dictionary<string, object?> cache = new(StringComparer.Ordinal);
    private readonly HashSet<string> visiting = new(StringComparer.Ordinal);
    private readonly Stack<Binding> bindings = new();
    private IReadOnlyList<object?>? summaryValues;
    private int activeDepth;

    public NativeBaseEvaluator(
        NativeBaseFile current,
        NativeBaseFile baseFile,
        IReadOnlyDictionary<string, NativeBaseExpression>? formulaDefinitions = null,
        IReadOnlyDictionary<string, NativeBaseFile>? sourceFiles = null,
        NativeBaseBudget? evaluationBudget = null,
        long? currentTime = null
    )
    {
        file = current;
        context = baseFile;
        formulas = formulaDefinitions ?? new Dictionary<string, NativeBaseExpression>(StringComparer.Ordinal);
        files = sourceFiles
            ?? new Dictionary<string, NativeBaseFile>(StringComparer.Ordinal) { [current.Path] = current };
        budget = evaluationBudget ?? new NativeBaseBudget();
        now = currentTime ?? DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    public object? Evaluate(string source) => Evaluate(NativeBaseExpression.Parse(source));

    public object? Evaluate(NativeBaseExpression expression) => EvaluateNode(expression);

    public object? Property(string property)
    {
        var separator = property.IndexOf('.');
        var owner = separator < 0 ? "note" : property[..separator];
        if (owner is not ("note" or "file" or "formula"))
        {
            owner = "note";
            separator = -1;
        }
        var key = separator < 0 ? property : property[(separator + 1)..];
        return Member(Name(owner), key);
    }

    public object? EvaluateSummary(NativeBaseExpression expression, IReadOnlyList<object?> values)
    {
        if (values.Count > 10_000)
        {
            throw new InvalidDataException("A summary supports at most 10,000 values.");
        }
        if (summaryValues is not null)
        {
            throw new InvalidOperationException("A summary evaluation is already active.");
        }
        summaryValues = values;
        try
        {
            return Evaluate(expression);
        }
        finally
        {
            summaryValues = null;
            bindings.Clear();
        }
    }

    private object? EvaluateNode(NativeBaseExpression expression)
    {
        budget.Spend();
        if (++activeDepth > 96)
        {
            activeDepth--;
            throw new InvalidDataException("Bases query exceeded its evaluation depth.");
        }
        try
        {
            return Dispatch(expression);
        }
        finally
        {
            activeDepth--;
        }
    }

    private object? Dispatch(NativeBaseExpression expression) =>
        expression switch
        {
            NativeBaseExpression.Literal literal => literal.Value,
            NativeBaseExpression.Name name => Name(name.Value),
            NativeBaseExpression.List list => list.Items.Select(EvaluateNode).ToArray(),
            NativeBaseExpression.Member member => Member(EvaluateNode(member.Receiver), EvaluateNode(member.Key)),
            NativeBaseExpression.Unary unary => Unary(unary.Operator, EvaluateNode(unary.Operand)),
            NativeBaseExpression.Binary binary => Binary(binary, EvaluateNode),
            NativeBaseExpression.Invocation invocation => Invoke(invocation, EvaluateNode),
            _ => throw new InvalidDataException("Unsupported Bases expression."),
        };

    private object? Name(string name)
    {
        NativeBaseValues.CheckName(name);
        if (bindings.TryPeek(out var binding))
        {
            if (name == "value")
            {
                return binding.Value;
            }
            if (name == "index")
            {
                return (double)binding.Index;
            }
            if (name == "acc")
            {
                return binding.Accumulator;
            }
        }
        if (summaryValues is not null)
        {
            return name == "values" ? summaryValues : null;
        }
        return name switch
        {
            "file" => file,
            "note" => ReadProperties(file),
            "this" => context,
            "formula" => ReferenceKind.Formulas,
            _ => NoteProperty(file, name),
        };
    }

    private object? Member(object? receiver, object? key)
    {
        var name = key is string text ? text : NativeBaseValues.Number(key).ToString("G", CultureInfo.InvariantCulture);
        NativeBaseValues.CheckName(name);
        return receiver switch
        {
            ReferenceKind.Formulas => Formula(name),
            NativeBaseFile source => FileField(source, name),
            NativeBaseDate date => DateField(date, name),
            string value when name == "length" => (double)value.Length,
            IReadOnlyList<object?> list => ListField(list, name),
            IReadOnlyDictionary<string, object?> properties => DictionaryField(properties, name),
            _ => null,
        };
    }

    private object? DictionaryField(IReadOnlyDictionary<string, object?> properties, string name)
    {
        if (ReferenceEquals(properties, file.Properties))
        {
            return NoteProperty(file, name);
        }
        if (ReferenceEquals(properties, context.Properties))
        {
            return NoteProperty(context, name);
        }
        return properties.GetValueOrDefault(name);
    }

    private object? Formula(string name)
    {
        if (cache.TryGetValue(name, out var cached))
        {
            return cached;
        }
        if (!visiting.Add(name))
        {
            throw new InvalidDataException("Circular formula reference: " + name);
        }
        try
        {
            if (!formulas.TryGetValue(name, out var expression))
            {
                throw new InvalidDataException("Unknown formula: " + name);
            }
            var value = Evaluate(expression);
            cache.Add(name, value);
            return value;
        }
        finally
        {
            visiting.Remove(name);
        }
    }

    private static IReadOnlyDictionary<string, object?> ReadProperties(NativeBaseFile source)
    {
        if (source.MetadataError is not null)
        {
            throw new InvalidDataException("This note's metadata is unavailable: " + source.MetadataError);
        }
        return source.Properties;
    }

    private static object? NoteProperty(NativeBaseFile source, string name)
    {
        var value = ReadProperties(source).GetValueOrDefault(name);
        if (
            value is not null
            && source.PropertyTypes.GetValueOrDefault(name.ToLowerInvariant()) is "date" or "datetime"
        )
        {
            return NativeBaseDates.Parse(NativeBaseValues.Text(value));
        }
        return NativeBaseValues.IsNumber(value) ? NativeBaseValues.Number(value) : value;
    }

    private static object? FileField(NativeBaseFile source, string name) =>
        name switch
        {
            "file" => source,
            "note" or "properties" => ReadProperties(source),
            "name" => source.File.Name,
            "basename" => source.File.Basename,
            "path" => source.Path,
            "ext" => source.File.Extension,
            "folder" => System.IO.Path.GetDirectoryName(source.Path)?.Replace('\\', '/') ?? "",
            "size" => NativeBaseValues.Number(source.File.Stat.Size),
            "ctime" => source.File.Stat.Ctime > 0 ? NativeBaseDates.Create(source.File.Stat.Ctime) : null,
            "mtime" => source.File.Stat.Mtime > 0 ? NativeBaseDates.Create(source.File.Stat.Mtime) : null,
            "tags" => source.Tags.Select(tag => (object?)tag.TrimStart('#')).ToArray(),
            "links" => source
                .Links.Select(path => (object?)new NativeBaseLink(path, WithoutMarkdownExtension(path)))
                .ToArray(),
            _ => throw new InvalidDataException("Unsupported file field: " + name),
        };

    private static object? ListField(IReadOnlyList<object?> list, string name)
    {
        if (name == "length")
        {
            return (double)list.Count;
        }
        return int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < list.Count
            ? list[index]
            : null;
    }

    private static double DateField(NativeBaseDate value, string name)
    {
        var date = value.Local;
        return name switch
        {
            "year" => date.Year,
            "month" => date.Month,
            "day" => date.Day,
            "hour" => date.Hour,
            "minute" => date.Minute,
            "second" => date.Second,
            "millisecond" => date.Millisecond,
            _ => throw new InvalidDataException("Unsupported date field: " + name),
        };
    }

    private static object Unary(string operation, object? value) =>
        operation switch
        {
            "!" => !NativeBaseValues.Truth(value),
            "-" => -NativeBaseValues.Number(value),
            "+" => NativeBaseValues.Number(value),
            _ => throw new InvalidDataException("Unsupported unary operator."),
        };

    private object? Binary(NativeBaseExpression.Binary expression, Func<NativeBaseExpression, object?> run)
    {
        var left = run(expression.Left);
        if (expression.Operator == "&&")
        {
            return NativeBaseValues.Truth(left) ? run(expression.Right) : left;
        }
        if (expression.Operator == "||")
        {
            return NativeBaseValues.Truth(left) ? left : run(expression.Right);
        }
        var right = run(expression.Right);
        return expression.Operator switch
        {
            "==" => NativeBaseValues.Equal(left, right),
            "!=" => !NativeBaseValues.Equal(left, right),
            ">" => CompareMatching(left, right) > 0,
            ">=" => CompareMatching(left, right) >= 0,
            "<" => CompareMatching(left, right) < 0,
            "<=" => CompareMatching(left, right) <= 0,
            "-" when left is NativeBaseDate a && right is NativeBaseDate b => (double)(a.Milliseconds - b.Milliseconds),
            _ => Arithmetic(expression.Operator, left, right),
        };
    }

    private object? Arithmetic(string operation, object? left, object? right)
    {
        if (summaryValues is not null && (left is null || right is null))
        {
            return null;
        }
        if (left is NativeBaseDate date && right is string duration && operation is "+" or "-")
        {
            return NativeBaseDates.Add(date, duration, operation == "-" ? -1 : 1);
        }
        if (operation == "+" && (left is string || right is string))
        {
            return NativeBaseValues.BoundedText(NativeBaseValues.ScalarText(left) + NativeBaseValues.ScalarText(right));
        }
        var first = NativeBaseValues.Number(left);
        var second = NativeBaseValues.Number(right);
        return NativeBaseValues.Number(
            operation switch
            {
                "+" => first + second,
                "-" => first - second,
                "*" => first * second,
                "/" => first / second,
                "%" => first % second,
                _ => throw new InvalidDataException("Unsupported arithmetic operator."),
            }
        );
    }

    private static int CompareMatching(object? left, object? right)
    {
        if (left is string a && right is string b)
        {
            return string.CompareOrdinal(a, b);
        }
        if (left is NativeBaseDate x && right is NativeBaseDate y)
        {
            return x.Milliseconds.CompareTo(y.Milliseconds);
        }
        if (NativeBaseValues.IsNumber(left) && NativeBaseValues.IsNumber(right))
        {
            return NativeBaseValues.Number(left).CompareTo(NativeBaseValues.Number(right));
        }
        throw new InvalidDataException("Compare matching numbers, strings or dates.");
    }

    private object? Invoke(NativeBaseExpression.Invocation invocation, Func<NativeBaseExpression, object?> run)
    {
        if (invocation.Callee is NativeBaseExpression.Name { Value: "if" })
        {
            if (invocation.Arguments.Count is < 2 or > 3)
            {
                throw new InvalidDataException("if() needs two or three arguments.");
            }
            if (NativeBaseValues.Truth(run(invocation.Arguments[0])))
            {
                return run(invocation.Arguments[1]);
            }
            return invocation.Arguments.Count == 3 ? run(invocation.Arguments[2]) : null;
        }
        if (invocation.Callee is NativeBaseExpression.Member member)
        {
            var receiver = run(member.Receiver);
            var name = NativeBaseValues.Text(run(member.Key));
            if (receiver is IReadOnlyList<object?> list && name is "map" or "filter" or "reduce")
            {
                return Transform(list, name, invocation.Arguments, run);
            }
            return Method(receiver, name, invocation.Arguments.Select(run).ToArray());
        }
        if (invocation.Callee is NativeBaseExpression.Name global)
        {
            return Global(global.Value, invocation.Arguments.Select(run).ToArray());
        }
        throw new InvalidDataException("Only supported Bases functions can be called.");
    }

    private object? Transform(
        IReadOnlyList<object?> values,
        string name,
        IReadOnlyList<NativeBaseExpression> arguments,
        Func<NativeBaseExpression, object?> run
    )
    {
        if (arguments.Count != (name == "reduce" ? 2 : 1))
        {
            throw new InvalidDataException(name + "() has an invalid argument count.");
        }
        CheckList(values);
        var accumulator = name == "reduce" ? run(arguments[1]) : null;
        var result = new List<object?>();
        for (var index = 0; index < values.Count; index++)
        {
            bindings.Push(new Binding(values[index], index, accumulator));
            object? next;
            try
            {
                next = run(arguments[0]);
            }
            finally
            {
                bindings.Pop();
            }
            if (name == "reduce")
            {
                accumulator = next;
            }
            else if (name == "map")
            {
                result.Add(next);
            }
            else if (NativeBaseValues.Truth(next))
            {
                result.Add(values[index]);
            }
        }
        return name == "reduce" ? accumulator : result;
    }

    private void CheckList(IReadOnlyList<object?> values)
    {
        var limit = summaryValues is null ? 1024 : 10_000;
        if (values.Count > limit)
        {
            throw new InvalidDataException(
                "Formula lists exceed " + limit.ToString(CultureInfo.InvariantCulture) + " elements."
            );
        }
        budget.Spend(values.Count);
    }

    private static string WithoutMarkdownExtension(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? path[..^3] : path;
}
