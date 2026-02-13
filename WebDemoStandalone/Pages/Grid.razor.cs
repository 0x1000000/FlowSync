using FlowSync;
using Microsoft.AspNetCore.Components;

namespace WebDemoStandalone.Pages;

public partial class Grid
{
    private readonly GridDataProvider _dataProvider = new();

    public readonly RequestTracker RequestTracker = new();

    [Parameter]
    public IFlowSyncStrategy<int>? SyncStrategy
    {
        get;
        set;
    }

    [Parameter] 
    public string Header { get; set; }

    public string GroupFilter { get; set; } = string.Empty;

    public string SortBy { get; set; } = GridDataProvider.OrderBys[0].Name;

    public IReadOnlyList<GridDataItem> Items = [];

    protected override async Task OnInitializedAsync()
    {
        await this.RefreshGridData();
    }

    public void StartRefreshing()
    {
        if (this.SyncStrategy == null)
        {
            Task.Run(this.RefreshGridData);
        }
        else
        {
            this.RefreshGridDataFixed()
                .CoalesceInDefaultGroupUsing(this.SyncStrategy)
                .Start();
        }

    }

    private async Task RefreshGridData()
    {
        var (requestId, isLong) = this.RequestTracker.LogRequestStart(this.GroupFilter, this.SortBy);

        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            this.Items = await this._dataProvider.Request(isLong, this.GroupFilter, this.SortBy, CancellationToken.None);

            this.RequestTracker.LogRequestEnd(requestId, RequestTracker.LogStatus.Completed);
        }
        catch
        {
            this.RequestTracker.LogRequestEnd(requestId, RequestTracker.LogStatus.Error);
        }

        await this.InvokeAsync(this.StateHasChanged);
    }

    private async FlowSyncTask<int> RefreshGridDataFixed()
    {
        var cancellationContext = await FlowSyncTask.GetCancellationContext();

        var (requestId, isLong) = this.RequestTracker.LogRequestStart(this.GroupFilter, this.SortBy);

        await this.InvokeAsync(this.StateHasChanged);

        try
        {
            this.Items = await this._dataProvider.Request(isLong, this.GroupFilter, this.SortBy, cancellationContext.CancellationToken);

            this.RequestTracker.LogRequestEnd(requestId, RequestTracker.LogStatus.Completed);
        }
        catch (OperationCanceledException)
        {
            this.RequestTracker.LogRequestEnd(requestId, RequestTracker.LogStatus.Cancelled);
        }
        catch
        {
            this.RequestTracker.LogRequestEnd(requestId, RequestTracker.LogStatus.Error);
        }

        await this.InvokeAsync(this.StateHasChanged);

        return requestId;
    }

    private string GetStatusStyle(RequestTracker.LogItem row)
    {
        if (row.Status is RequestTracker.LogStatus.Error or RequestTracker.LogStatus.Cancelled)
        {
            return "status-row-warning";
        }
        else if(row.Status is RequestTracker.LogStatus.Completed)
        {
            return "status-row-good";
        }

        return string.Empty;
    }
}

public class GridDataItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
}

public class GridDataProvider
{
    public async Task<IReadOnlyList<GridDataItem>> Request(
        bool isLong,
        string filterByGroup,
        string orderBy,
        CancellationToken cancellationToken)
    {
        await Task.Delay(isLong ? 7000 : 2000, cancellationToken);

        var result = GenerateTestData();

        if (!string.IsNullOrEmpty(filterByGroup))
        {
            result = result.Where(x => x.Group == filterByGroup);
        }

        if (!string.IsNullOrEmpty(orderBy))
        {
            result = OrderBys.FirstOrDefault(x => x.Name == orderBy).M(result);
        }


        return result.ToList();
    }

    public static readonly IReadOnlyList<string> Groups = new[] { "Group A", "Group B", "Group C", "Group D", "Group E" };

    public static readonly IReadOnlyList<string> Names = new[]
    {
        "Alpha", "Beta", "Gamma", "Delta", "Epsilon",
        "Zeta", "Eta", "Theta", "Iota", "Kappa",
        "Lambda", "Mu", "Nu", "Xi", "Omicron",
        "Pi", "Rho", "Sigma", "Tau", "Upsilon"
    };

    public static readonly IReadOnlyList<(string Name, Func<IEnumerable<GridDataItem>, IEnumerable<GridDataItem>> M)> OrderBys =
        new (string Name, Func<IEnumerable<GridDataItem>, IEnumerable<GridDataItem>> M)[]
        {
            ($"{nameof(GridDataItem.Name)} ASC", e => e.OrderBy(x => x.Name)),
            ($"{nameof(GridDataItem.Name)} DESC", e => e.OrderByDescending(x => x.Name)),
            ($"{nameof(GridDataItem.Group)} ASC", e => e.OrderBy(x => x.Group)),
            ($"{nameof(GridDataItem.Group)} DESC", e => e.OrderByDescending(x => x.Group))
        };

    public static IEnumerable<GridDataItem> GenerateTestData(int count = 100)
    {
        var random = new Random();

        for (int i = 1; i <= count; i++)
        {
            yield return new GridDataItem
            {
                Id = i,
                Name = $"{Names[random.Next(Names.Count)]}-{i}",
                Group = Groups[random.Next(Groups.Count)]
            };
        }
    }
}

public class RequestTracker
{
    public enum LogStatus
    {
        Started,
        Completed,
        Error,
        Cancelled
    }

    public record LogItem(int Id, string Filter, string Sorting, LogStatus Status, int? CompletionOrder);

    private int _completionCounter;

    private readonly List<LogItem> _log = new();

    private int _logVersion;

    private List<LogItem> _logCopy = new();

    private int _logCopyVersion;

    public int IsLoading;

    public IReadOnlyList<LogItem> Log
    {
        get
        {
            lock (this._log)
            {
                if (this._logVersion != this._logCopyVersion)
                {
                    this._logCopy = this._log.ToList();
                    this._logCopyVersion = this._logVersion;
                }

                return this._logCopy;
            }
        }
    }

    public (int RequestId, bool HasSortingChanged) LogRequestStart(string groupFilter, string sortBy)
    {
        int requestId;
        bool hasSortingChanged = false;
        lock (this._log)
        {
            if (this._log.Count > 0)
            {
                hasSortingChanged = sortBy != this._log[^1].Sorting;
            }

            this.IsLoading++;
            requestId = this._log.Count + 1;
            this._log.Add(
                new LogItem(requestId, groupFilter, sortBy, LogStatus.Started, null)
            );
            this._logVersion++;
        }

        return (requestId, hasSortingChanged);
    }

    public void LogRequestEnd(int requestId, LogStatus status)
    {
        lock (this._log)
        {
            this._completionCounter++;
            this.IsLoading--;
            var i = this._log.FindLastIndex(x => x.Id == requestId);
            this._log[i] = this._log[i] with { Status = status, CompletionOrder = this._completionCounter };
            this._logVersion++;
        }
    }
}
