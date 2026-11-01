namespace Vigil.Presentation;

/// <summary>Asks before a destructive action: a message dialog with the action and Cancel (the default).</summary>
public static class Confirmation
{
    public static async ValueTask<bool> ConfirmAsync(this INavigator navigator, object sender, string content, string title, string action, CancellationToken ct)
    {
        var confirmed = false;
        // The choice comes back through DialogAction callbacks, not the awaited result (runtime gotcha).
        await navigator.ShowMessageDialogAsync<object>(
            sender,
            content: content,
            title: title,
            defaultButtonIndex: 1,
            cancelButtonIndex: 1,
            buttons:
            [
                new DialogAction(action, () => confirmed = true),
                new DialogAction(Loc.T("Common_Cancel", "Cancel")),
            ],
            cancellation: ct);
        return confirmed;
    }
}
