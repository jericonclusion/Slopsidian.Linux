namespace RonSijm.Slopsidian.Linux;

public enum NativeErrorAction
{
    OpenVault,
    EnableAssistant,
    EnableTerminal,
}

public sealed class NativeActionRequiredException(string message, NativeErrorAction action)
    : InvalidOperationException(message)
{
    public NativeErrorAction Action { get; } = action;
}
