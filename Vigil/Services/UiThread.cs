using Microsoft.UI.Dispatching;

namespace Vigil.Services;

/// <summary>
/// Runs work on the window's UI thread. Pickers and the clipboard need it, and MVUX commands may run elsewhere.
/// The queue is captured at launch: an injected IDispatcher is window-scoped and resolves null in a singleton.
/// </summary>
public static class UiThread
{
    public static DispatcherQueue? Queue { get; set; }

    /// <summary>The main window. WinAppSDK pickers need its handle as their owner.</summary>
    public static Window? Window { get; set; }

    public static Task<T> RunAsync<T>(Func<Task<T>> work)
    {
        if (Queue is null || Queue.HasThreadAccess)
        {
            return work();
        }
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!Queue.TryEnqueue(async () =>
            {
                try
                {
                    tcs.SetResult(await work());
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }))
        {
            tcs.SetException(new InvalidOperationException("The UI thread is not available."));
        }
        return tcs.Task;
    }
}
