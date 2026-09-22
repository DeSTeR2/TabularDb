using Microsoft.AspNetCore.Components;
using TabularDb.Web.Services;

namespace TabularDb.Web.Shared;

public abstract class ApiComponentBase : ComponentBase
{
    [Inject]
    protected TabularApiClient Api { get; set; } = null!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = null!;

    protected bool Busy { get; private set; }

    protected ApiException? Error { get; set; }

    protected string? Info { get; set; }

    protected async Task<bool> RunAsync(Func<Task> action)
    {
        if (Busy)
            return false;
        Busy = true;
        Error = null;
        Info = null;
        StateHasChanged();
        try
        {
            await action();
            return true;
        }
        catch (ApiException ex)
        {
            Error = ex;
            return false;
        }
        finally
        {
            Busy = false;
            StateHasChanged();
        }
    }
}
