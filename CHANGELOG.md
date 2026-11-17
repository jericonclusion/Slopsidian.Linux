# Changelog

## 0.3.0-preview.3

- Add configurable accent colors with a native color picker, live preview and
  restore-to-default behavior in Appearance settings.
- Add subtle neutral Gradient dark/Gradient light presets and a black/green Matrix
  preset, retaining flat Dark/Light and system appearance.
- Persist appearance choices and restore the previous look when settings are
  cancelled; keep terminal text readable across all presets.

- Add contextual recovery buttons to native error messages: open/create a vault
  directly, or jump to Native modules settings for a disabled assistant/terminal.
- Keep errors when recovery is cancelled, prevent duplicate dialogs and retain
  newer failures when a previous recovery completes.
- Bound long error messages so recovery and dismiss controls remain reachable.
- Add native keyboard, vault creation/opening and recovery-state regression tests.

## 0.3.0-preview.2

- Add independent CLI assistant and Terminal switches under Settings → Native modules.
- Persist module choices immediately, including before opening a vault; retain CLI
  provider settings and leave vault plugin configuration unchanged.
- Add a native interactive Linux terminal backed by a real PTY, with Unicode, ANSI
  rendering, resizing, copy/paste, interrupt and explicit session restart.
- Start the shell in the current vault (or home); end its session on terminal close,
  module disable or application exit.
- Add real-shell and native-control tests, terminal screenshots and setup guidance.
- Include the terminal component and dependency license texts in Linux packages.

## 0.3.0-preview.1

- Add a native assistant for installed Codex CLI, Claude Code and GitHub Copilot CLI.
- Attach frozen note drafts, source selections, linked notes and backlinks; inspect
  messages before sending and follow exact note/heading/block citations.
- Support follow-ups, provider-specific executable/model settings, cancellation,
  copied replies and guarded save-as-new-note actions.
- Bound context, process output and execution time; retain failed requests and
  cancel assistant work when its window closes or the vault changes.
- Add tested Bases formula, YAML editing and filtered/grouped query foundations.
  The native Bases table interface remains unfinished and is not exposed by this release.
- Focus README and contribution guidance on Slopsidian Linux, with assistant setup,
  screenshots and current capability limits documented separately.

## 0.2.0-preview.3

- Use GitHub-compatible package filenames and matching download/checksum links.
- Keep Debian prerelease ordering inside the package while using SemVer filenames.
- Provide ready-to-run x64/arm64 packages through GitHub Releases and README downloads.

## 0.2.0-preview.2

- Establish Slopsidian Linux as an independent fork with Linux-only source and CI.
- Target .NET 10 with Avalonia 12 and retain an independent client version.
- Add native Canvas editing, an inspector, note-section previews and image cards.
- Add a native read-only image preview window and bounded, disposable media loading.
- Retain native vault editing, search, metadata, graph, recovery and trash workflows.
- Include the revised Tux notebook icon, screenshots, CSharpier and strict analyzers.
- Build self-contained x64/arm64 archives and Debian packages; prepare draft releases.

The port remains a preview. Full upstream feature and plugin parity is unfinished.

## 0.2.0-preview.1

Initial independent version line for the native Linux client. Historical work
remains in the Git history; no public release is implied by a version heading.
