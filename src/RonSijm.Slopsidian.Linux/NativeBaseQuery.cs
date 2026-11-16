namespace RonSijm.Slopsidian.Linux;

public sealed record NativeBaseColumn(string Key, string Label, NativeBaseSummaryKind SummaryKind);

public sealed record NativeBaseCell(string Text, string? Error = null, NativeBaseLink? Link = null);

public sealed record NativeBaseSummaryCell(string Name, string Text, string? Error = null);

public sealed record NativeBaseRow(string Path, IReadOnlyList<NativeBaseCell> Cells, string Group, string GroupKey);

public sealed record NativeBaseGroup(string Key, string Label, int Count)
{
    public IReadOnlyList<NativeBaseSummaryCell?>? Summaries { get; init; }
}

public sealed record NativeBaseResult(IReadOnlyList<NativeBaseColumn> Columns, IReadOnlyList<NativeBaseRow> Rows)
{
    public int Total { get; init; }
    public int Matched { get; init; }
    public int Page { get; init; }
    public int Pages { get; init; }
    public IReadOnlyList<NativeBaseSummaryCell?> Summaries { get; init; } = [];
    public IReadOnlyList<NativeBaseGroup> Groups { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record NativeBaseQueryRequest(
    NativeBaseDocument Document,
    IReadOnlyList<NativeBaseFile> Files,
    NativeBaseFile Context
)
{
    public string View { get; init; } = "Table";
    public string Search { get; init; } = "";
    public int Page { get; init; }
    public long? CurrentTime { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public static partial class NativeBaseQuery
{
    private sealed record SourceRow(
        string Path,
        Dictionary<string, object?> Values,
        Dictionary<string, string> Failures,
        object? Group
    )
    {
        public string GroupKey { get; set; } = "";
    }

    private sealed record SourceGroup(string Key, string Label, object? Value, int Start)
    {
        public int Count { get; set; } = 1;
    }

    public static NativeBaseResult Execute(NativeBaseQueryRequest request) => new Execution(request).Run();

    private sealed partial class Execution
    {
        private readonly NativeBaseQueryRequest request;
        private readonly NativeBaseDocument document;
        private readonly NativeBaseView view;
        private readonly Dictionary<string, NativeBaseFile> files = new(StringComparer.Ordinal);
        private readonly NativeBaseBudget budget;
        private readonly List<string> errors = [];
        private readonly string[] keys;
        private readonly string search;
        private readonly long now;

        public Execution(NativeBaseQueryRequest request)
        {
            if (request.Files.Count > 10_000)
            {
                throw new InvalidDataException("Bases supports at most 10,000 files per query.");
            }
            this.request = request;
            budget = new NativeBaseBudget(cancellationToken: request.CancellationToken);
            document = request.Document;
            view = document.View(request.View);
            if (view.Type != "table")
            {
                throw new InvalidDataException(
                    "The "
                        + view.Type
                        + " view is retained but not implemented. Choose a table view or edit its source."
                );
            }
            foreach (var file in request.Files)
            {
                files[file.Path] = file;
            }
            keys = view
                .Order.Concat(view.Sort.Select(sort => sort.Property))
                .Concat(view.GroupBy is null ? [] : new[] { view.GroupBy.Property })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var term = request.Search.Trim();
            search = term.Length > 1024 ? term[..1024] : term;
            now = request.CurrentTime ?? DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        public NativeBaseResult Run()
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            var rows = new List<SourceRow>();
            foreach (var file in files.Values)
            {
                var row = EvaluateRow(file);
                if (row is not null)
                {
                    rows.Add(row);
                }
            }
            SortRows(rows);
            var matched = rows.Count;
            var limited = view.Limit is { } limit ? rows.Take(limit).ToArray() : rows.ToArray();
            var columns = view
                .Order.Select(key => new NativeBaseColumn(
                    key,
                    document.Label(key),
                    NativeBaseSummaries.Kind(
                        limited
                            .Select(row => row.Values.GetValueOrDefault(key))
                            .FirstOrDefault(value => value is not null)
                    )
                ))
                .ToArray();
            var pages = Math.Max(1, (int)Math.Ceiling(limited.Length / 100d));
            var page = Math.Clamp(request.Page, 0, pages - 1);
            var groups = GroupRows(limited);
            var visible = limited.Skip(page * 100).Take(100).ToArray();
            var visibleGroups = visible.Select(row => row.GroupKey).ToHashSet(StringComparer.Ordinal);
            return new NativeBaseResult(columns, visible.Select(BuildRow).ToArray())
            {
                Total = limited.Length,
                Matched = matched,
                Page = page,
                Pages = pages,
                Summaries = Summarize(columns, limited, "All rows"),
                Groups = groups
                    .Select(group => new NativeBaseGroup(group.Key, group.Label, group.Count)
                    {
                        Summaries =
                            groups.Count > 1 && visibleGroups.Contains(group.Key)
                                ? Summarize(
                                    columns,
                                    limited.AsSpan(group.Start, group.Count).ToArray(),
                                    "Group " + group.Label
                                )
                                : null,
                    })
                    .ToArray(),
                Errors = errors.ToArray(),
                Warnings = view
                    .Warnings.Concat(
                        document.FormulaErrors.Select(entry => "Formula " + entry.Key + ": " + entry.Value)
                    )
                    .ToArray(),
            };
        }

        private SourceRow? EvaluateRow(NativeBaseFile file)
        {
            var evaluator = Evaluator(file);
            if (!Matches(file, evaluator))
            {
                return null;
            }
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            var failures = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                EvaluateProperty(file, evaluator, key, values, failures);
            }
            foreach (var key in view.Order)
            {
                ValidateDisplay(file.Path, key, values, failures);
            }
            if (
                search.Length > 0
                && !view.Order.Any(key =>
                    Display(key, values, failures).Contains(search, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return null;
            }
            var group = view.GroupBy is null ? null : values.GetValueOrDefault(view.GroupBy.Property);
            if (
                group is not (null or string or bool or NativeBaseDate or NativeBaseLink)
                && !NativeBaseValues.IsNumber(group)
            )
            {
                throw new InvalidDataException(
                    "Grouping structured/list properties is not implemented. Choose a scalar, date or link property."
                );
            }
            return new SourceRow(file.Path, values, failures, group);
        }

        private bool Matches(NativeBaseFile file, NativeBaseEvaluator evaluator)
        {
            try
            {
                return (document.Filter?.Matches(evaluator, budget) ?? true)
                    && (view.Filter?.Matches(evaluator, budget) ?? true);
            }
            catch (InvalidDataException exception)
            {
                if (budget.Remaining < 0)
                {
                    throw;
                }
                Issue(file.Path + ": " + exception.Message);
                return false;
            }
        }

        private void EvaluateProperty(
            NativeBaseFile file,
            NativeBaseEvaluator evaluator,
            string key,
            Dictionary<string, object?> values,
            Dictionary<string, string> failures
        )
        {
            try
            {
                var property = NativeBaseDocument.Property(key);
                if (
                    property.Kind == NativeBasePropertyKind.Formula
                    && document.FormulaErrors.TryGetValue(property.Name, out var error)
                )
                {
                    throw new InvalidDataException(error);
                }
                values[key] = evaluator.Property(property.Canonical);
            }
            catch (InvalidDataException exception)
            {
                if (budget.Remaining < 0)
                {
                    throw;
                }
                failures[key] = exception.Message;
                Issue(file.Path + " / " + key + ": " + exception.Message);
            }
        }

        private void ValidateDisplay(
            string path,
            string key,
            Dictionary<string, object?> values,
            Dictionary<string, string> failures
        )
        {
            if (failures.ContainsKey(key))
            {
                return;
            }
            try
            {
                _ = NativeBaseValues.Display(values.GetValueOrDefault(key));
            }
            catch (InvalidDataException exception)
            {
                failures[key] = exception.Message;
                Issue(path + " / " + key + ": " + exception.Message);
            }
        }

        private static string Display(
            string key,
            Dictionary<string, object?> values,
            Dictionary<string, string> failures
        ) => failures.ContainsKey(key) ? "#ERROR" : NativeBaseValues.Display(values.GetValueOrDefault(key));

        private NativeBaseRow BuildRow(SourceRow row) =>
            new(
                row.Path,
                view.Order.Select(key => BuildCell(row, key)).ToArray(),
                view.GroupBy is null ? "" : GroupLabel(row.Group),
                row.GroupKey
            );

        private static NativeBaseCell BuildCell(SourceRow row, string key)
        {
            if (row.Failures.TryGetValue(key, out var error))
            {
                return new NativeBaseCell("#ERROR", error);
            }
            var value = row.Values.GetValueOrDefault(key);
            var link = value as NativeBaseLink;
            if (key is "file.name" or "file.basename" or "file.path")
            {
                link = new NativeBaseLink(row.Path, NativeBaseValues.Display(value)) { ExactPath = true };
            }
            return new NativeBaseCell(NativeBaseValues.Display(value), Link: link);
        }

        private void SortRows(List<SourceRow> rows)
        {
            try
            {
                rows.Sort(CompareRows);
            }
            catch (InvalidOperationException exception) when (exception.InnerException is OperationCanceledException)
            {
                throw new OperationCanceledException(
                    "Bases query was canceled.",
                    exception.InnerException,
                    request.CancellationToken
                );
            }
            catch (InvalidOperationException exception) when (exception.InnerException is InvalidDataException)
            {
                throw new InvalidDataException(exception.InnerException.Message, exception.InnerException);
            }
        }

        private int CompareRows(SourceRow left, SourceRow right)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            if (view.GroupBy is not null)
            {
                var group = NativeBaseValues.Compare(left.Group, right.Group);
                if (group != 0)
                {
                    return group * Direction(view.GroupBy);
                }
            }
            foreach (var sort in view.Sort)
            {
                var comparison = NativeBaseValues.Compare(
                    left.Values.GetValueOrDefault(sort.Property),
                    right.Values.GetValueOrDefault(sort.Property)
                );
                if (comparison != 0)
                {
                    return comparison * Direction(sort);
                }
            }
            return NativeBaseValues.Compare(left.Path, right.Path);
        }

        private List<SourceGroup> GroupRows(SourceRow[] rows)
        {
            var result = new List<SourceGroup>();
            if (view.GroupBy is null)
            {
                return result;
            }
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                var previous = result.LastOrDefault();
                if (previous is not null && NativeBaseValues.Equal(previous.Value, row.Group))
                {
                    previous.Count++;
                    row.GroupKey = previous.Key;
                }
                else
                {
                    row.GroupKey = "group-" + result.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    result.Add(new SourceGroup(row.GroupKey, GroupLabel(row.Group), row.Group, index));
                }
            }
            return result;
        }

        private NativeBaseEvaluator Evaluator(NativeBaseFile file) =>
            new(file, request.Context, document.Formulas, files, budget, now);

        private static int Direction(NativeBaseSort sort) => sort.Direction == "DESC" ? -1 : 1;

        private static string GroupLabel(object? value) =>
            NativeBaseValues.Display(value) is { Length: > 0 } text ? text : "No value";

        private void Issue(string message)
        {
            if (errors.Count < 50)
            {
                errors.Add(message);
            }
        }
    }
}
