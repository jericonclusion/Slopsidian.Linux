namespace RonSijm.Slopsidian.Linux;

public partial class MainWindow
{
    public async Task SetNativeModuleEnabledAsync(NativeModule module, bool enabled)
    {
        if (!profileWritable)
        {
            throw new InvalidOperationException(
                "The native profile cannot be saved. Resolve the profile error before changing modules."
            );
        }
        var previous = profile.State.Preferences.IsModuleEnabled(module);
        profile.State = profile.State with { Preferences = profile.State.Preferences.WithModule(module, enabled) };
        try
        {
            await PersistProfileAsync();
        }
        catch
        {
            profile.State = profile.State with { Preferences = profile.State.Preferences.WithModule(module, previous) };
            throw;
        }
        ApplyNativeModuleAvailability();
    }

    private void ApplyNativeModuleAvailability()
    {
        var enabled = profile.State.Preferences.CliAssistantEnabled;
        AssistantButton.IsVisible = enabled;
        AssistantButton.IsEnabled = enabled;
        if (!enabled)
        {
            assistantWindow?.Close();
        }
        TerminalButton.IsVisible = profile.State.Preferences.TerminalEnabled;
        TerminalButton.IsEnabled = TerminalButton.IsVisible;
        if (!profile.State.Preferences.TerminalEnabled)
        {
            terminalWindow?.Close();
        }
    }
}
