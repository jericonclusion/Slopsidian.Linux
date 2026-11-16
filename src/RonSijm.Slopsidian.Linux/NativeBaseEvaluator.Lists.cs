using System.Runtime.CompilerServices;

namespace RonSijm.Slopsidian.Linux;

public sealed partial class NativeBaseEvaluator
{
    private sealed class ValueEquality(NativeBaseBudget budget) : IEqualityComparer<object?>
    {
        public new bool Equals(object? left, object? right) => NativeBaseValues.Equal(left, right);

        public int GetHashCode(object? value) => Hash(value, 0);

        private int Hash(object? value, int depth)
        {
            budget.Spend();
            if (depth > 32)
            {
                throw new InvalidDataException("A value is nested too deeply to compare.");
            }
            return value switch
            {
                null => 0,
                NativeBaseDate date => HashCode.Combine("date", date.Milliseconds),
                NativeBaseLink link => HashCode.Combine("link", StringComparer.Ordinal.GetHashCode(link.Path)),
                string text => HashCode.Combine("string", StringComparer.Ordinal.GetHashCode(text)),
                bool boolean => HashCode.Combine("boolean", boolean),
                IReadOnlyList<object?> list => ListHash(list, depth),
                _ when NativeBaseValues.IsNumber(value) => HashCode.Combine("number", NativeBaseValues.Number(value)),
                _ => RuntimeHelpers.GetHashCode(value),
            };
        }

        private int ListHash(IReadOnlyList<object?> values, int depth)
        {
            var hash = new HashCode();
            hash.Add("list");
            hash.Add(values.Count);
            foreach (var value in values)
            {
                hash.Add(Hash(value, depth + 1));
            }
            return hash.ToHashCode();
        }
    }

    private static readonly IComparer<object?> ValueOrder = Comparer<object?>.Create(NativeBaseValues.Compare);

    private object? ListMethod(IReadOnlyList<object?> values, string name, object?[] arguments)
    {
        if (name == "join" && arguments.Length <= 1)
        {
            return Join(
                values,
                arguments.Length == 0 || arguments[0] is null ? "," : NativeBaseValues.Text(arguments[0])
            );
        }
        if (name == "slice")
        {
            var (start, count) = SliceRange(values.Count, arguments);
            return values.Skip(start).Take(count).ToArray();
        }
        if (arguments.Length != 0)
        {
            throw Unsupported(name);
        }
        return name switch
        {
            "sum" or "mean" or "min" or "max" or "median" or "stddev" => NumericAggregate(values, name),
            "earliest" or "latest" => DateAggregate(values, name),
            "reverse" => values.Reverse().ToArray(),
            "sort" => Sort(values),
            "unique" => values.Distinct(new ValueEquality(budget)).ToArray(),
            _ => throw Unsupported(name),
        };
    }

    private double? NumericAggregate(IReadOnlyList<object?> values, string name)
    {
        var numbers = values.Where(NativeBaseValues.IsNumber).Select(NativeBaseValues.Number).ToArray();
        if (numbers.Length == 0)
        {
            return null;
        }
        if (name == "min")
        {
            return numbers.Min();
        }
        if (name == "max")
        {
            return numbers.Max();
        }
        if (name == "median")
        {
            return Median(numbers);
        }
        var sum = numbers.Aggregate(0d, (total, value) => NativeBaseValues.Number(total + value));
        if (name == "sum")
        {
            return sum;
        }
        // The upstream formula divides by the whole input list, including nulls.
        if (name == "mean")
        {
            return NativeBaseValues.Number(sum / values.Count);
        }
        var mean = sum / numbers.Length;
        var variance = numbers.Sum(value => Math.Pow(value - mean, 2) / numbers.Length);
        return NativeBaseValues.Number(Math.Sqrt(variance));
    }

    private double Median(double[] values)
    {
        SortBudget(values.Length);
        Array.Sort(values);
        var middle = values.Length / 2;
        return values.Length % 2 == 1
            ? values[middle]
            : NativeBaseValues.Number(values[middle - 1] / 2 + values[middle] / 2);
    }

    private static NativeBaseDate? DateAggregate(IReadOnlyList<object?> values, string name)
    {
        var dates = new List<long>();
        foreach (var value in values)
        {
            if (value is NativeBaseDate date)
            {
                dates.Add(NativeBaseDates.Create(date.Milliseconds).Milliseconds);
            }
            else if (value is string text)
            {
                try
                {
                    dates.Add(NativeBaseDates.Parse(text).Milliseconds);
                }
                catch (InvalidDataException)
                { /* Non-date strings do not participate in a date summary. */
                }
            }
        }
        if (dates.Count == 0)
        {
            return null;
        }
        return NativeBaseDates.Create(name == "earliest" ? dates.Min() : dates.Max());
    }

    private object?[] Sort(IReadOnlyList<object?> values)
    {
        SortBudget(values.Count);
        return values.OrderBy(value => value, ValueOrder).ToArray();
    }

    private void SortBudget(int count) => budget.Spend((int)Math.Ceiling(count * Math.Log2(count + 1)));

    private static string Join(IReadOnlyList<object?> values, string separator)
    {
        var result = new System.Text.StringBuilder();
        for (var index = 0; index < values.Count; index++)
        {
            if (index > 0)
            {
                Append(result, separator);
            }
            Append(result, NativeBaseValues.ScalarText(values[index]));
        }
        return result.ToString();
    }
}
