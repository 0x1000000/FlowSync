using FlowSync;
using Microsoft.AspNetCore.Components;

namespace WebDemoStandalone.Controls;

public enum ProgressState
{
    Initial,
    Pending,
    InProgress,
    Redirected,
    Completed,
    RedirectedCompleted,
    Cancelled
}

public partial class RequestProgress
{
    [Parameter]
    public string ResourceId { get; set; } = string.Empty;

    [Parameter]
    public int RequestIndex { get; set; }

    [Parameter]
    public IFlowSyncStrategy<int> SyncStrategy { get; set; } = default!;

    [Parameter]
    public EventCallback<int> ResultChanged { get; set; }

    private int ProgressPrc { get; set; }

    private int LorryProgress { get; set; }

    private ProgressState ProgressState { get; set; }

    private int Result { get; set; }

    private string ProgressStatusDescription => this.ProgressState switch
    {
        ProgressState.Initial => "Not Started",
        ProgressState.Pending => "Pending",
        ProgressState.InProgress => $"Progress: {this.ProgressPrc}%...",
        ProgressState.Redirected => "Redirected. Waiting for external result...",
        ProgressState.Completed => $"Completed with result: {this.Result}",
        ProgressState.RedirectedCompleted => $"Cancelled with external result: {this.Result}",
        ProgressState.Cancelled => "Cancelled",
        _ => throw new ArgumentOutOfRangeException()
    };

    private bool IsStartDisabled
        => this.ProgressState is ProgressState.InProgress or ProgressState.Redirected or ProgressState.Pending;

    public bool IsClearCancelDisabled => this.ProgressState == ProgressState.Redirected ||
                                         (this.ProgressState == ProgressState.InProgress && this.ProgressPrc == 100);

    public bool IsClearCancelVisible => this.ProgressState is not ProgressState.Initial;

    public bool IsCancelable  => this.ProgressState is ProgressState.InProgress or ProgressState.Pending or ProgressState.Redirected;

    public int? ResultBox => this.ProgressState is ProgressState.Completed or ProgressState.RedirectedCompleted
        ? this.Result
        : null;

    public bool IsLorryActive => this.ProgressState is ProgressState.InProgress;

    private async Task Start()
    {
        if (this.IsStartDisabled)
        {
            return;
        }

        this.ProgressState = ProgressState.Pending;
        this.ProgressPrc = 0;
        this.LorryProgress = 0;
        try
        {
            this.Result = await this.GetResultAsync()
                .CoalesceUsing(this.SyncStrategy, this.ResourceId);

            await this.ResultChanged.InvokeAsync(this.Result);

            this.ProgressState = this.ProgressState == ProgressState.Redirected
                ? ProgressState.RedirectedCompleted
                : ProgressState.Completed;
        }
        catch (OperationCanceledException)
        {
            this.ProgressState = ProgressState.Cancelled;
        }
        finally
        {
            this.ProgressPrc = 100;
        }

        await this.InvokeAsync(this.StateHasChanged);
    }

    private async FlowSyncTask<int> GetResultAsync()
    {
        this.ProgressState = ProgressState.InProgress;
        await this.InvokeAsync(this.StateHasChanged);

        var cancellationContext = await FlowSyncTask.GetCancellationContext();

        try
        {
            var result = this.RequestIndex;

            for (var i = 1; i <= 100; i++)
            {
                await Task.Delay(54 - this.RequestIndex*3, cancellationContext.CancellationToken);
                this.ProgressPrc = i;
                this.LorryProgress = i;
                await this.InvokeAsync(this.StateHasChanged);
            }
            return result;
        }
        catch (OperationCanceledException) when (cancellationContext.IsCancelledLocally)
        {
            this.ProgressState = ProgressState.Redirected;
            await this.InvokeAsync(this.StateHasChanged);
            return -1;
        }
    }

    private void ClearCancel()
    {
        if (this.IsCancelable)
        {
            this.SyncStrategy.Cancel(this.ResourceId);
        }
        else
        {
            this.ProgressPrc = 0;
            this.LorryProgress = 0;
            this.ProgressState = ProgressState.Initial;
        }
    }
}