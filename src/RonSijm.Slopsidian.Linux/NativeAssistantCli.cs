using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace RonSijm.Slopsidian.Linux;

public enum NativeAssistantProvider
{
    Codex,
    Claude,
    Copilot,
}

public sealed record NativeAssistantProviderSettings(string Executable, string Model = "");

public sealed record NativeAssistantSettings
{
    public NativeAssistantProvider Provider { get; init; }
    public NativeAssistantProviderSettings Codex { get; init; } = new("codex");
    public NativeAssistantProviderSettings Claude { get; init; } = new("claude");
    public NativeAssistantProviderSettings Copilot { get; init; } = new("copilot");

    public NativeAssistantProviderSettings For(NativeAssistantProvider provider) =>
        provider switch
        {
            NativeAssistantProvider.Codex => Codex,
            NativeAssistantProvider.Claude => Claude,
            NativeAssistantProvider.Copilot => Copilot,
            _ => throw new InvalidDataException("Choose Codex, Claude Code or GitHub Copilot CLI."),
        };

    public NativeAssistantSettings With(NativeAssistantProvider provider, NativeAssistantProviderSettings value) =>
        provider switch
        {
            NativeAssistantProvider.Codex => this with { Provider = provider, Codex = value },
            NativeAssistantProvider.Claude => this with { Provider = provider, Claude = value },
            NativeAssistantProvider.Copilot => this with { Provider = provider, Copilot = value },
            _ => throw new InvalidDataException("Choose a supported CLI provider."),
        };

    public static void Validate(NativeAssistantSettings settings)
    {
        foreach (var provider in Enum.GetValues<NativeAssistantProvider>())
        {
            var value = settings.For(provider);
            if (
                value is null
                || string.IsNullOrWhiteSpace(value.Executable)
                || value.Executable.Length > 4096
                || value.Executable.IndexOfAny(['\0', '\r', '\n']) >= 0
                || value.Model is null
                || value.Model.Length > 256
                || value.Model.IndexOfAny(['\0', '\r', '\n']) >= 0
            )
            {
                throw new InvalidDataException("The assistant's executable or model setting is invalid.");
            }
        }
        _ = settings.For(settings.Provider);
    }
}

/// <summary>Installed CLI processes with stdin requests, bounded pipes and cancellation of the process tree.</summary>
public static class NativeAssistantCli
{
    public const int MaximumAnswerCharacters = 64_000;
    private const int MaximumDiagnosticsCharacters = 16_000;

    public static ProcessStartInfo StartInfo(
        NativeAssistantProvider provider,
        NativeAssistantProviderSettings settings,
        string workingDirectory
    )
    {
        NativeAssistantSettings.Validate(new NativeAssistantSettings().With(provider, settings));
        if (
            settings.Executable.Contains(Path.DirectorySeparatorChar) && !Path.IsPathFullyQualified(settings.Executable)
        )
        {
            throw new InvalidDataException("Use a command from PATH or an absolute executable path.");
        }
        var info = new ProcessStartInfo
        {
            FileName = settings.Executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        string[] arguments = provider switch
        {
            NativeAssistantProvider.Codex =>
            [
                "--no-daemon",
                "--ask-for-approval",
                "never",
                "exec",
                "--ignore-user-config",
                "--ephemeral",
                "--skip-git-repo-check",
                "--sandbox",
                "read-only",
                "--color",
                "never",
                "--disable",
                "apps",
                "--disable",
                "plugins",
                "--disable",
                "hooks",
                "--disable",
                "shell_tool",
                "--disable",
                "unified_exec",
                "--disable",
                "multi_agent",
                "--config",
                "web_search=\"disabled\"",
                "--config",
                "tools.view_image=false",
            ],
            NativeAssistantProvider.Claude =>
            [
                "--print",
                "--output-format",
                "text",
                "--safe-mode",
                "--tools",
                "",
                "--disallowedTools",
                "mcp__*",
                "--permission-mode",
                "dontAsk",
                "--strict-mcp-config",
                "--mcp-config",
                "{\"mcpServers\":{}}",
                "--no-session-persistence",
            ],
            NativeAssistantProvider.Copilot =>
            [
                "--silent",
                "--stream",
                "on",
                "--no-color",
                "--no-auto-update",
                "--no-ask-user",
                "--no-custom-instructions",
                "--disable-builtin-mcps",
                "--disable-client-telemetry",
                "--available-tools=view,glob,grep",
                "--allow-tool=read",
                "--deny-tool=write",
                "--deny-tool=shell",
                "--no-remote",
                "--no-remote-export",
                "--trust-folder",
            ],
            _ => throw new InvalidDataException("Unsupported CLI provider."),
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }
        if (!string.IsNullOrWhiteSpace(settings.Model))
        {
            info.ArgumentList.Add("--model");
            info.ArgumentList.Add(settings.Model);
        }
        if (provider == NativeAssistantProvider.Codex)
        {
            info.ArgumentList.Add("-");
        }
        info.Environment["NO_COLOR"] = "1";
        info.Environment["TERM"] = "dumb";
        return info;
    }

    public static async Task<string> RunAsync(
        NativeAssistantProvider provider,
        NativeAssistantProviderSettings settings,
        string message,
        Action<string> output,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default
    )
    {
        if (message.Length > NativeAssistantContext.MaximumMessageCharacters)
        {
            throw new InvalidDataException("The assistant message is too large.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        // An empty private directory avoids discovering instructions or project hooks in a user's vault.
        var directory = Path.Combine(Path.GetTempPath(), "slopsidian-assistant-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsLinux())
        {
            Directory.CreateDirectory(
                directory,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        }
        else
        {
            Directory.CreateDirectory(directory);
        }
        try
        {
            return await RunProcessAsync(
                StartInfo(provider, settings, directory),
                message,
                output,
                timeout ?? TimeSpan.FromMinutes(10),
                cancellationToken
            );
        }
        finally
        {
            // No note snapshots are written here. Do not recursively follow anything created by a CLI.
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            { /* A CLI may still hold a temporary file; it contains no app-written note data. */
            }
            catch (UnauthorizedAccessException)
            { /* Retain a provider-owned file rather than failing a completed turn. */
            }
        }
    }

    private static async Task<string> RunProcessAsync(
        ProcessStartInfo info,
        string message,
        Action<string> output,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        using var process = new Process { StartInfo = info };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(
                "Cannot start "
                    + info.FileName
                    + ". Install and sign in to this CLI in a terminal, or set its absolute executable path.",
                exception
            );
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var cleanup = deadline.Token.Register(() => Stop(process));
        var answer = ReadPipeAsync(process.StandardOutput, MaximumAnswerCharacters, output, deadline);
        var diagnostics = ReadPipeAsync(process.StandardError, MaximumDiagnosticsCharacters, null, deadline);
        try
        {
            await process.StandardInput.WriteAsync(message.AsMemory(), deadline.Token);
            process.StandardInput.Close();
            await Task.WhenAll(answer, diagnostics, process.WaitForExitAsync(deadline.Token));
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"CLI exited with code {process.ExitCode}. " + diagnostics.Result.Trim()
                );
            }
            if (string.IsNullOrWhiteSpace(answer.Result))
            {
                throw new InvalidOperationException(
                    "The CLI returned no answer. Check its login, model and CLI version in a terminal. "
                        + diagnostics.Result.Trim()
                );
            }
            return answer.Result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "The CLI did not finish within the time limit. You can retry or use a smaller request."
            );
        }
        finally
        {
            Stop(process);
            await deadline.CancelAsync();
            // Observe both pipes even when the CLI closes stdin before accepting the request.
            try
            {
                await Task.WhenAll(answer, diagnostics);
            }
            catch (Exception exception)
                when (exception is OperationCanceledException or IOException or InvalidDataException)
            {
                // The original failure or cancellation is already being propagated to the caller.
            }
        }
    }

    private static async Task<string> ReadPipeAsync(
        StreamReader reader,
        int maximum,
        Action<string>? output,
        CancellationTokenSource deadline
    )
    {
        var text = new StringBuilder();
        var buffer = new char[2048];
        try
        {
            while (true)
            {
                var count = await reader.ReadAsync(buffer.AsMemory(), deadline.Token);
                if (count == 0)
                {
                    return text.ToString();
                }
                if (text.Length + count > maximum)
                {
                    throw new InvalidDataException(
                        "The CLI output exceeded its supported size. Start a new conversation with a smaller request."
                    );
                }
                var chunk = new string(buffer, 0, count);
                text.Append(chunk);
                output?.Invoke(chunk);
            }
        }
        catch
        {
            await deadline.CancelAsync();
            throw;
        }
    }

    private static void Stop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        { /* The process exited between the check and kill. */
        }
        catch (Win32Exception)
        { /* It may already have been reaped by the OS. */
        }
    }
}
