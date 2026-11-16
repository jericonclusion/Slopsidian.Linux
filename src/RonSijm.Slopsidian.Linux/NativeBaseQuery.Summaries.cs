namespace RonSijm.Slopsidian.Linux;

public static partial class NativeBaseQuery
{
    private sealed partial class Execution
    {
        private NativeBaseSummaryCell?[] Summarize(NativeBaseColumn[] columns, SourceRow[] rows, string scope) =>
            columns.Select(column => SummarizeColumn(column, rows, scope)).ToArray();

        private NativeBaseSummaryCell? SummarizeColumn(NativeBaseColumn column, SourceRow[] rows, string scope)
        {
            var name = document.Summary(view.Name, column.Key);
            if (name is null)
            {
                return null;
            }
            try
            {
                if (document.SummaryErrors.TryGetValue(name, out var error))
                {
                    throw new InvalidDataException(error);
                }
                var syntax = SummaryExpression(name, column.Key, rows);
                if (rows.Any(row => row.Failures.ContainsKey(column.Key)))
                {
                    throw new InvalidDataException(
                        "Some values could not be evaluated. Fix the column's cell errors before summarizing it."
                    );
                }
                var values = rows.Select(row => row.Values.GetValueOrDefault(column.Key)).ToArray();
                var value = Evaluator(request.Context).EvaluateSummary(syntax, values);
                if (
                    value is not (null or string or bool or NativeBaseDate or NativeBaseLink)
                    && !NativeBaseValues.IsNumber(value)
                )
                {
                    throw new InvalidDataException("A summary must return a single scalar, date or link value.");
                }
                return new NativeBaseSummaryCell(name, value is null ? "No value" : NativeBaseValues.Display(value));
            }
            catch (InvalidDataException exception)
            {
                if (budget.Remaining < 0)
                {
                    throw;
                }
                Issue(scope + " / " + column.Key + " / " + name + ": " + exception.Message);
                return new NativeBaseSummaryCell(name, "#ERROR", exception.Message);
            }
        }

        private NativeBaseExpression SummaryExpression(string name, string property, SourceRow[] rows)
        {
            if (document.SummaryFormulas.TryGetValue(name, out var custom))
            {
                return custom;
            }
            var first = rows.Select(row => row.Values.GetValueOrDefault(property))
                .FirstOrDefault(value => value is not null);
            var formula = NativeBaseSummaries.Definition(NativeBaseSummaries.Kind(first), name, first is null)
                ?? throw new InvalidDataException("Unknown summary or incompatible column type: " + name + ".");
            return NativeBaseExpression.Parse(formula);
        }
    }
}
