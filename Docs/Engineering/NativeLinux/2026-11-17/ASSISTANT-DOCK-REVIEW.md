# Native assistant refinement and terminal docking

Prepared for **0.3.0-preview.4**. This record covers the native UI implementation
at ccc3a521f1a18e669b41bf2b7ccc8592f65b48f2 on
feat/assistant-workspace-terminal-dock. Release metadata preparation follows the
reviewed implementation; this record makes no publication or CI-success claim.

## Result and scope

The final independent finish review returned **disposition: ship**. Its scope
was the requested assistant refinement and terminal docking within the existing
Avalonia desktop interface. It covered the compact provider/conversation/settings
toolbar, transcript and reply actions, inspectable snapshot attachments,
composer/review/Stop/retry/error states, and the resizable dock and detached
terminal. It did not expand into unrelated native-port gaps or pre-existing
design-document drift.

The review's five contract sections recorded:

| Section | Final result |
| --- | --- |
| persistence | “Pass within scope. DESIGN.md and .impeccable/design.json preserve the incumbent native system. All 14 required captures were opened and are valid for their named states and dimensions. All 15 capture-manifest source/assembly hashes match current files. No recapture required. Pre-existing product-document gaps remain outside this review.” |
| fidelity | The refinement retains the incumbent native palette, fonts, icon family and Fluent behavior. The compact settings arrangement is a justified adaptation for this assistant surface. |
| ceiling | “Reached for the requested native refinement.” |
| material_fixes | “None within the requested assistant refinement and terminal-docking scope.” |
| keep | “Preserve inspectable snapshots, restrained transcript actions, reachable Stop/retry controls and the same live terminal session across docking transfers.” |

## Behavior

The assistant keeps provider selection, New conversation and CLI settings in a
compact toolbar. The transcript gives replies readable space and restrained
copy/save actions. Context is shown as named snapshot rows with individual
inspection and removal; inspection displays the exact attached content. The
composer keeps message review, Send, running Stop and recoverable status/error
feedback reachable. The task remains usable at 860×860 and 560×680, including
long settings errors and scrollable settings content.

Terminal controls offer Dock at bottom and Open in window. Presentation changes
move the same native pane and terminal control, preserving the live PTY,
emulator/buffer and current directory. Closing the terminal ends that session;
starting a new session remains an explicit action. The dock shares the editor
workspace and has a draggable divider. The saved dock preference and height are
restored while the layout clamps height to leave note content usable in small
windows.

This remains a Linux Avalonia 12.1.6 / .NET 10 desktop implementation. No HTML,
Electron, WebView or JavaScript host is involved.

## Verification

- Local ./tools/lint-native.sh: **PASS**, zero warnings/errors, with pinned
  CSharpier 1.4, style checks, recommended .NET analyzers and SonarAnalyzer.
- Full Release xUnit v3/VSTest suite: **378 passed, zero failed/skipped, 29s**.
  Added coverage comprises six real-PTY docking/lifecycle/persistence/failure/
  layout/shortcut cases and one compact assistant context/settings interaction
  case. Existing assistant safety, provider/citation/export/cancellation and
  appearance coverage remains in the suite.
- A real mouse drag on the private native display changed dock height from
  **280 to 350** logical units; the profile remembered **350**.
- Dock/window transfers retained the same live shell PID and the same terminal
  emulator and directory. The capture report records live PID 1262724.
- Synthetic note SHA256s were unchanged after capture.
- The 15 entries in
  .impeccable/review/workspace/capture-source-hashes.json were rechecked during
  documentation and all matched the then-current source and Release app assembly.
  The assembly SHA256 was
  7184759b815a52fd6f7728c990784f22f69a0993c38368febc1ae7dba7b9d92d.

Behavior-sensitive source checks included NativeAssistantLayout.axaml,
NativeAssistantWindow.Context.cs, NativeTerminalLayout.axaml,
NativeTerminalPane.cs and MainWindow.Terminal.cs under
src/RonSijm.Slopsidian.Linux. The terminal binds its solid background to
TerminalSurfaceBrush and foreground to TextBrush; moving the pane brackets
reparenting with BeginReparent/EndReparent. Session startup is separate from
presentation changes.

## Native capture evidence

All 14 required frames captured in the temporary
.impeccable/review/workspace/frames directory were opened by the
final reviewer and accepted for their named states and dimensions. They cover
empty, Dark/Light conversation, reply actions, narrow context, long settings
error at both scroll positions, running/Stop, stopped/retry, wide/narrow/Matrix
docking, and detached Dark/Light terminal states. Conversation captures show
real scroll positions; a single viewport does not contain every turn.

The capture harness ran the current compiled native app on a private Xvfb
display, using a 2000×1200×24 screen and real native windows. Its runner checked
the capture app's assembly against the current Release app assembly before
launching. The windows were captured at 860×860 or 560×680 for the assistant,
1400×950 or 760×520 for the workspace, and 1000×700 for the detached terminal.
No user desktop input or paid model request was used. Notes, paths and CLI
responses are synthetic fixtures; the transcript and vault explicitly label
their demonstration content.

Eight representative images are retained in
Docs/Engineering/NativeLinux/2026-11-17/Screenshots. Each was opened during this
documentation pass and verified as a byte-for-byte copy of its current scratch
frame. Screenshots/README.md records the original frame, dimensions, byte size,
SHA256 and state for each copy. The other seven PNGs in that directory belong
to the earlier error-action and appearance reviews and are not evidence of this
assistant/docking pass. The full capture matrix, geometry report and source
manifest were verified in the temporary .impeccable/review/workspace directory
during review. Those scratch files are subject to release cleanup; the eight
representative images and these review records are the retained evidence.

No web detector applies to native XAML, so no web-detector result is claimed.
Terminal foreground defaults and solid surfaces inherit the tested theme
resources; shell-selected ANSI colors remain program output with their existing
behavior.

## System preservation

DESIGN.md and .impeccable/design.json were compared with the current native
implementation and left unchanged. All 46 documented color-token values are
present in App.axaml. Window defaults still use Segoe UI, Noto Sans, sans-serif
at 14 logical units; the terminal retains DejaVu Sans Mono, monospace at 14.
Assistant metadata and tool actions use the existing 12/13-unit supporting
sizes, alongside the 20-unit assistant empty-state heading. Fluent still owns
control interaction and focus treatment, and the existing NativeIcon geometry
is reused. The terminal continues to follow the documented Solid Terminal Rule.

The assistant's 24-unit inset, bounded settings/context/status scroll regions
and compact settings fields are local layout choices for this task. They do not
establish new application-wide spacing, type, breakpoint, shadow or motion
tokens. The existing palette, semantic resource roles and appearance behavior
remain the system authority.

The design files retain their historical bb1ac4e appearance-extraction
provenance. The sidecar's terminal source locator still names
NativeTerminalWindow.cs; terminal construction now resides in
NativeTerminalPane.cs. These historical locators and pre-existing document gaps
are reported without regenerating the incumbent system. No task-specific
layout adaptation or defect is canonized into a new global rule. PRODUCT.md
was already absent; this scoped refinement does not initialize a product or
replace the visual identity.

Preserved file SHA256s:

- DESIGN.md: a5eca99d64e3d8cf1920821998162111c5432a871e664cc0678edd2c9e859ab8
- .impeccable/design.json: 0896600e8ad71e8f4f47948b331670e874a14b0a9d1ad9ac7eea3508ae2618f8
