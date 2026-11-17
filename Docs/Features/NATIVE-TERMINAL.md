# Native terminal

Open **Terminal** from the ribbon or **Terminal: Open an interactive shell** in
the command palette. It opens in a resizable panel below the editor by default.
The terminal uses a real Linux PTY and native Avalonia text
rendering through Iciclecreek.Avalonia.Terminal/XTerm.NET and Porta.Pty. It hosts
no Electron, WebView, browser terminal or JavaScript runtime.

## Shell and directory

The shell starts in the current vault. With no vault open, it starts in your
home directory. An existing session keeps its directory when you switch vaults;
the header records its starting directory. Use normal shell `cd` commands to
change directory. Opening Terminal again focuses the same session, whether it is
docked or in a separate window.

An existing absolute `$SHELL` path is used, with `/bin/bash` or `/bin/sh` as the
fallback. The shell is interactive and uses its normal startup configuration.
The application passes the executable, arguments and directory separately.

This is an ordinary shell with your user permissions. Commands can read or
change files and access the network. The CLI assistant remains a separate
read-and-suggest feature with explicitly attached note context.

## Dock or open in a window

Choose **Open in window** in the terminal header to detach it. Choose **Dock at
bottom** in the detached window to put it back below the editor. Both actions
move the same native pane: shell process, current directory, scrollback and
running commands stay intact.

Drag the horizontal divider above the terminal to resize it. The app remembers
the last height and dock/window choice in its private profile. Smaller windows
limit the dock height to leave room for the editor. The choice affects the next
explicit Terminal command; it never opens a terminal or starts a shell on app
startup. A failed preference save keeps the existing presentation and session.

## Controls and lifecycle

- Keyboard input belongs to the terminal while its surface has focus; editor
  shortcuts do not intercept shell keys.
- **Ctrl+C** interrupts a foreground command; **Ctrl+D** sends end-of-input.
- **Copy** or **Ctrl+Shift+C** copies selected terminal text.
- **Paste** or **Ctrl+Shift+V** sends clipboard text to the shell. Pasted commands
  execute according to the shell/application’s usual behavior.
- Resizing updates the PTY dimensions. Unicode and ANSI terminal output render
  natively; scrollback retains up to 2,000 lines.
- **End session** ends the shell and releases its PTY. **New session** starts a
  fresh terminal buffer after the previous session ends. Exiting a shell never
  automatically starts another.
- Closing the terminal, disabling its module or closing Slopsidian ends the
  owned shell session. Processes deliberately detached from the terminal by
  commands such as `nohup` or `disown` can continue, as in an ordinary terminal.

Terminal output is kept in memory for that session; Slopsidian does not save a
terminal transcript. Your shell’s history and programs’ own logs still apply.

## Enable or disable

Use **Settings → Native modules → Terminal**. Choices apply immediately and
persist in the private app profile, including without an open vault. Disabling
hides its ribbon/command-palette entries and ends its session. Enabling makes
the command available without opening a window or starting a shell.

The assistant has its own independent switch on the same page. Both built-in
modules are available by default, including for older profiles. These switches
do not provide compatibility with Obsidian community JavaScript plugins and do
not change vault core-plugin configuration.

If profile saving fails, the previous module choice stays active and Settings
shows the error. Fix the profile path/permissions before retrying.

## Verification

Real-shell tests use temporary directories and `/bin/bash --noprofile --norc -i`
to avoid developer startup scripts. They cover PTY input, Unicode, Ctrl+C, exit,
restart, live dock/window transfers, post-transfer input and directory state,
minimum window layout, shell-owned shortcuts, close/disable cleanup and failed
preference persistence. Native controls also test
module persistence, command availability, pending saves and save rollback.
Screenshots use a synthetic vault on a private X server.
