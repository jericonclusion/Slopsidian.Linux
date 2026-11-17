using Avalonia.Automation;
using Avalonia.Interactivity;
using RonSijm.Slopsidian.Services;

namespace RonSijm.Slopsidian.Linux;

public partial class MainWindow
{
    private NativeErrorAction? errorAction;
    private long errorRevision;
    private bool recoveringError;

    private void ShowError(Exception exception)
    {
        errorRevision++;
        errorAction = exception switch
        {
            NativeActionRequiredException required => required.Action,
            VaultException { VaultRequired: true } => NativeErrorAction.OpenVault,
            _ => null,
        };
        ErrorText.Text = exception.Message;
        ErrorActionText.Text = errorAction == NativeErrorAction.OpenVault ? "Open vault…" : "Open settings…";
        ErrorActionIcon.Kind = errorAction == NativeErrorAction.OpenVault ? "folder" : "settings";
        AutomationProperties.SetName(ErrorActionButton, ErrorActionText.Text);
        ErrorActionButton.IsVisible = errorAction.HasValue;
        ErrorActionButton.IsEnabled = !busy && !recoveringError;
        ErrorPanel.IsVisible = true;
    }

    private void ClearError()
    {
        errorRevision++;
        errorAction = null;
        ErrorPanel.IsVisible = false;
        ErrorActionButton.IsVisible = false;
    }

    private void DismissError(object? sender, RoutedEventArgs args) => ClearError();

    private async void RecoverError(object? sender, RoutedEventArgs args)
    {
        if (busy || recoveringError || errorAction is not { } action)
        {
            return;
        }
        var shownRevision = errorRevision;
        recoveringError = true;
        ErrorActionButton.IsEnabled = false;
        try
        {
            if (action == NativeErrorAction.OpenVault)
            {
                await ChooseVaultAsync(clearError: false);
            }
            else
            {
                await OpenSettingsAsync(nativeModules: true);
            }
            var resolved = action switch
            {
                NativeErrorAction.OpenVault => Workspace.Snapshot.Root is not null,
                NativeErrorAction.EnableAssistant => profile.State.Preferences.CliAssistantEnabled,
                NativeErrorAction.EnableTerminal => profile.State.Preferences.TerminalEnabled,
                _ => false,
            };
            if (resolved && shownRevision == errorRevision)
            {
                ClearError();
            }
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
        finally
        {
            recoveringError = false;
            ErrorActionButton.IsEnabled = !busy;
        }
    }
}
